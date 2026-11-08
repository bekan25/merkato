using System.Security.Claims;
using System.Text.Json;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/returns")]
public sealed class ReturnsController(MerkatoDbContext db) : ControllerBase
{
    private long CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("mine")]
    [Authorize(Roles = "BUYER")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var rows = await (from request in db.ReturnRequests.AsNoTracking()
            join order in db.Orders.AsNoTracking() on request.OrderId equals order.Id
            where request.BuyerId == CurrentUserId
            orderby request.CreatedAt descending
            select new
            {
                request.Id, request.OrderId, request.Reason, request.Status, request.ResolutionNote,
                request.CreatedAt, request.UpdatedAt, order.TotalAmountEtb
            }).ToListAsync(ct);
        return Ok(rows.Select(row =>
        {
            var refund = SellerRefundHistory.Read(row.ResolutionNote, row.TotalAmountEtb, row.Status);
            return new
            {
                row.Id, row.OrderId, row.Reason,
                status = refund.RefundStatus == "NOT_REFUNDED" ? row.Status.ToString() : refund.RefundStatus,
                refundStatus = refund.RefundStatus,
                refundedAmountEtb = refund.RefundedAmountEtb,
                remainingRefundEtb = refund.RemainingRefundEtb,
                refundHistory = refund.RefundHistory,
                row.TotalAmountEtb, row.ResolutionNote, row.CreatedAt, row.UpdatedAt
            };
        }));
    }

    [HttpPost]
    [Authorize(Roles = "BUYER")]
    public async Task<IActionResult> RequestReturn(ReturnRequestInput input, CancellationToken ct)
    {
        var reason = input.Reason?.Trim();
        if (input.OrderId <= 0 || string.IsNullOrWhiteSpace(reason) || reason.Length > 1200)
            return BadRequest(new { message = "Provide a valid order and a return reason (up to 1200 characters)." });

        var order = await db.Orders.FirstOrDefaultAsync(
            x => x.Id == input.OrderId && x.BuyerId == CurrentUserId, ct);
        if (order is null) return NotFound();

        if (order.DeliveryStatus != DeliveryStatus.DELIVERED ||
            order.EscrowState is EscrowState.CANCELLED or EscrowState.REFUNDED)
            return Conflict(new { message = "A return can only be requested for a delivered, non-cancelled order." });
        if (order.EscrowState == EscrowState.RELEASED)
            return Conflict(new { message = "The escrow inspection period has ended. Contact MERKATO support to request a dispute review." });
        var delivery = await db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == order.Id, ct);
        var deliveredAt = delivery?.DeliveredAt ?? order.CreatedAt;
        if (deliveredAt < DateTime.UtcNow.AddDays(-7))
            return Conflict(new { message = "Returns must be requested within 7 days of delivery." });

        if (await db.ReturnRequests.AnyAsync(x => x.OrderId == order.Id, ct))
            return Conflict(new { message = "A return request already exists for this order." });

        var row = new ReturnRequest
        {
            OrderId = order.Id,
            BuyerId = CurrentUserId,
            SellerId = order.SellerId,
            Reason = reason,
            Status = ReturnStatus.REQUESTED,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ReturnRequests.Add(row);
        AddAudit("RETURN_REQUESTED", "ReturnRequest", row.Id.ToString(), new { row.OrderId, row.SellerId, row.Reason });
        await db.SaveChangesAsync(ct);

        return Accepted(new
        {
            row.Id, row.OrderId, status = row.Status.ToString(), row.CreatedAt,
            message = "Return request submitted for merchant review. No refund has been issued."
        });
    }

    // Merchant queue: only returns assigned to the authenticated merchant.
    [HttpGet("seller")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> SellerQueue(CancellationToken ct)
    {
        var sellerId = CurrentUserId;
        if (sellerId <= 0) return Unauthorized();

        var rows = await (
            from request in db.ReturnRequests.AsNoTracking()
            join order in db.Orders.AsNoTracking() on request.OrderId equals order.Id
            where request.SellerId == sellerId
            orderby request.CreatedAt descending
            select new
            {
                request.Id, request.OrderId, order.OrderNumber, request.BuyerId,
                request.Reason, request.Status, request.ResolutionNote,
                order.ProductTitle, order.Quantity, order.TotalAmountEtb,
                order.PaymentMode, request.CreatedAt, request.UpdatedAt
            }).ToListAsync(ct);

        return Ok(rows.Select(row =>
        {
            var refund = SellerRefundHistory.Read(row.ResolutionNote, row.TotalAmountEtb, row.Status);
            return new
            {
                row.Id, row.OrderId, row.OrderNumber, row.BuyerId, row.Reason,
                status = refund.RefundStatus == "NOT_REFUNDED" ? row.Status.ToString() : refund.RefundStatus,
                refundStatus = refund.RefundStatus,
                refundedAmountEtb = refund.RefundedAmountEtb,
                remainingRefundEtb = refund.RemainingRefundEtb,
                refundHistory = refund.RefundHistory,
                row.ResolutionNote, row.ProductTitle, row.Quantity, row.TotalAmountEtb,
                row.PaymentMode, row.CreatedAt, row.UpdatedAt
            };
        }));
    }

    [HttpPost("{id:long}/decision")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> Decide(long id, [FromBody] MerchantReturnDecisionInput input, CancellationToken ct)
    {
        var decision = input.Decision?.Trim().ToUpperInvariant();
        var note = input.Note?.Trim();
        if (decision is not ("APPROVE" or "REJECT"))
            return BadRequest(new { message = "Decision must be APPROVE or REJECT." });
        if (decision == "REJECT" && string.IsNullOrWhiteSpace(note))
            return BadRequest(new { message = "A reason is required when rejecting a return." });
        if (note?.Length > 1000)
            return BadRequest(new { message = "The note cannot exceed 1000 characters." });

        var request = await db.ReturnRequests.FirstOrDefaultAsync(
            x => x.Id == id && x.SellerId == CurrentUserId, ct);
        if (request is null) return NotFound();
        if (request.Status != ReturnStatus.REQUESTED)
            return Conflict(new { message = $"This return cannot be reviewed from status {request.Status}." });

        request.Status = decision == "APPROVE" ? ReturnStatus.APPROVED : ReturnStatus.REJECTED;
        request.ResolutionNote = note;
        request.UpdatedAt = DateTime.UtcNow;
        AddAudit("RETURN_" + decision + "D", "ReturnRequest", request.Id.ToString(),
            new { request.OrderId, request.BuyerId, note });
        await db.SaveChangesAsync(ct);

        return Ok(new { request.Id, request.OrderId, status = request.Status.ToString(), request.ResolutionNote, request.UpdatedAt });
    }

    [HttpPost("{id:long}/received")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> MarkReceived(long id, [FromBody] ReturnNoteInput input, CancellationToken ct)
    {
        var note = input.Note?.Trim();
        if (note?.Length > 1000)
            return BadRequest(new { message = "The note cannot exceed 1000 characters." });

        var request = await db.ReturnRequests.FirstOrDefaultAsync(
            x => x.Id == id && x.SellerId == CurrentUserId, ct);
        if (request is null) return NotFound();
        if (request.Status != ReturnStatus.APPROVED)
            return Conflict(new { message = "Only an approved return can be marked as received." });

        request.Status = ReturnStatus.RECEIVED;
        request.ResolutionNote = string.IsNullOrWhiteSpace(note) ? "Returned item received by merchant." : note;
        request.UpdatedAt = DateTime.UtcNow;
        AddAudit("RETURN_ITEM_RECEIVED", "ReturnRequest", request.Id.ToString(),
            new { request.OrderId, request.BuyerId, note = request.ResolutionNote });
        await db.SaveChangesAsync(ct);

        return Ok(new { request.Id, request.OrderId, status = request.Status.ToString(), request.UpdatedAt });
    }

    // Seller-direct refunds happen outside MERKATO. This endpoint records the merchant's
    // attestation and bank/mobile-money reference; it does not call a provider or move funds.
    [HttpPost("{id:long}/refund-confirmation")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> ConfirmSellerDirectRefund(
        long id, [FromBody] SellerDirectRefundConfirmation input, CancellationToken ct)
    {
        var reference = input.TransactionReference?.Trim();
        var note = input.Note?.Trim();
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 150)
            return BadRequest(new { message = "A bank or mobile-money refund transaction reference is required." });
        if (input.RefundAmountEtb <= 0m)
            return BadRequest(new { message = "Refund amount must be greater than zero." });
        if (note?.Length > 1000)
            return BadRequest(new { message = "The note cannot exceed 1000 characters." });

        var request = await db.ReturnRequests.FirstOrDefaultAsync(
            x => x.Id == id && x.SellerId == CurrentUserId, ct);
        if (request is null) return NotFound();
        if (request.Status != ReturnStatus.RECEIVED)
            return Conflict(new { message = "The returned item must be received and must not already be fully refunded." });

        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == request.OrderId, ct);
        if (order is null) return NotFound(new { message = "The linked order no longer exists." });
        if (order.PaymentMode != "SELLER_DIRECT")
            return Conflict(new { message = "This order uses escrow. Resolve its refund through the existing MERKATO dispute and escrow workflow." });

        var history = SellerRefundHistory.Read(request.ResolutionNote, order.TotalAmountEtb, request.Status);
        if (input.RefundAmountEtb > history.RemainingRefundEtb)
            return BadRequest(new
            {
                message = "Refund amount cannot exceed the remaining refundable order balance.",
                maximumRefundEtb = history.RemainingRefundEtb,
                alreadyRefundedEtb = history.RefundedAmountEtb
            });

        var duplicateReference = await db.PaymentReceipts.AsNoTracking()
            .AnyAsync(x => x.TransactionReference == reference, ct);
        var normalizedReference = reference.ToUpperInvariant();
        var refundReferenceKey = $"seller-direct-refund-reference:{normalizedReference}";
        if (duplicateReference || await db.IdempotencyRecords.AnyAsync(x => x.Key == refundReferenceKey, ct))
            return Conflict(new { message = "This refund transaction reference has already been recorded." });

        var refundEntry = new SellerRefundHistoryEntry(
            input.RefundAmountEtb, reference, note, CurrentUserId, DateTime.UtcNow);
        var nextHistory = history.RefundHistory.Append(refundEntry).ToArray();
        var refundedTotal = nextHistory.Sum(x => x.RefundAmountEtb);
        var refundStatus = refundedTotal >= order.TotalAmountEtb ? "FULLY_REFUNDED" : "PARTIALLY_REFUNDED";
        request.ResolutionNote = SellerRefundHistory.Serialize(nextHistory, order.TotalAmountEtb);
        request.Status = refundStatus == "FULLY_REFUNDED" ? ReturnStatus.REFUNDED : ReturnStatus.RECEIVED;
        request.UpdatedAt = DateTime.UtcNow;

        // Commission reversals follow each distinct merchant-reported refund. The refund
        // remains an attestation, not a provider-verified transfer.
        if (order.MerchantCommissionEtb > 0m &&
            await db.IdempotencyRecords.AnyAsync(x => x.Key == $"seller-direct-commission:{order.Id}", ct))
        {
            var alreadyReversed = await (from tx in db.LedgerTransactions.AsNoTracking()
                join entry in db.LedgerEntries.AsNoTracking() on tx.Id equals entry.LedgerTransactionId
                where tx.TransactionType == "SELLER_DIRECT_COMMISSION_REVERSAL" && tx.OrderId == order.Id &&
                      entry.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && entry.UserId == request.SellerId &&
                      entry.EntryType == LedgerEntryType.CREDIT
                select (decimal?)entry.AmountEtb).SumAsync(ct) ?? 0m;
            var reversalAmount = SellerCommissionLedger.CalculateRefundReversal(
                order.MerchantCommissionEtb, order.TotalAmountEtb, input.RefundAmountEtb, alreadyReversed);
            if (reversalAmount > 0m)
                db.LedgerTransactions.Add(SellerCommissionLedger.CreateReversal(order.Id, request.SellerId, reversalAmount, reference));
        }

        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Key = refundReferenceKey,
            RequestHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{request.Id}:{input.RefundAmountEtb}:{normalizedReference}"))),
            ResponseJson = JsonSerializer.Serialize(new
            {
                status = refundStatus,
                refundAmountEtb = input.RefundAmountEtb,
                refundedTotalEtb = refundedTotal
            }),
            StatusCode = 200,
            CreatedAt = DateTime.UtcNow
        });
        AddAudit(refundStatus == "FULLY_REFUNDED"
                ? "SELLER_DIRECT_REFUND_FULLY_REPORTED"
                : "SELLER_DIRECT_REFUND_PARTIALLY_REPORTED",
            "ReturnRequest", request.Id.ToString(),
            new
            {
                request.OrderId, request.BuyerId, input.RefundAmountEtb,
                refundedTotalEtb = refundedTotal, remainingRefundEtb = order.TotalAmountEtb - refundedTotal,
                transactionReference = reference, verifiedByProvider = false
            });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "This refund transaction reference has already been recorded or conflicts with an existing record." });
        }

        return Ok(new
        {
            request.Id,
            request.OrderId,
            status = refundStatus,
            refundStatus,
            refundAmountEtb = input.RefundAmountEtb,
            refundedAmountEtb = refundedTotal,
            remainingRefundEtb = Math.Max(0m, order.TotalAmountEtb - refundedTotal),
            refundHistory = nextHistory,
            currency = "ETB",
            transactionReference = reference,
            verifiedByProvider = false,
            message = refundStatus == "FULLY_REFUNDED"
                ? "The merchant reported the full refund as sent. MERKATO recorded the reference but has not independently verified or transferred the funds."
                : "The merchant reported a partial refund as sent. The remaining refundable balance is shown; MERKATO has not independently verified or transferred the funds."
        });
    }

    private void AddAudit(string action, string entityType, string entityId, object metadata)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = CurrentUserId,
            ActorRole = User.FindFirstValue(ClaimTypes.Role) ?? "UNKNOWN",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Result = "SUCCESS",
            MetadataJson = JsonSerializer.Serialize(metadata),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            TraceId = HttpContext.TraceIdentifier,
            CreatedAt = DateTime.UtcNow
        });
    }
}

public sealed record ReturnRequestInput(long OrderId, string Reason);
public sealed record MerchantReturnDecisionInput(string Decision, string? Note);
public sealed record ReturnNoteInput(string? Note);
public sealed record SellerDirectRefundConfirmation(decimal RefundAmountEtb, string TransactionReference, string? Note);

public sealed record SellerRefundHistoryEntry(
    decimal RefundAmountEtb,
    string TransactionReference,
    string? Note,
    long ConfirmedByMerchantId,
    DateTime ConfirmedAt);

public sealed record SellerRefundSummary(
    string RefundStatus,
    decimal RefundedAmountEtb,
    decimal RemainingRefundEtb,
    IReadOnlyList<SellerRefundHistoryEntry> RefundHistory);

public static class SellerRefundHistory
{
    public static SellerRefundSummary Read(string? resolutionNote, decimal orderTotalEtb, ReturnStatus status)
    {
        var entries = new List<SellerRefundHistoryEntry>();
        if (!string.IsNullOrWhiteSpace(resolutionNote))
        {
            try
            {
                using var document = JsonDocument.Parse(resolutionNote);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("refundHistory", out var history) &&
                    history.ValueKind == JsonValueKind.Array)
                {
                    entries = JsonSerializer.Deserialize<List<SellerRefundHistoryEntry>>(history.GetRawText()) ?? new();
                }
                else if (root.ValueKind == JsonValueKind.Object &&
                         root.TryGetProperty("refundAmountEtb", out var amount) &&
                         amount.TryGetDecimal(out var legacyAmount) && legacyAmount > 0m)
                {
                    var reference = root.TryGetProperty("transactionReference", out var refValue)
                        ? refValue.GetString() ?? "LEGACY-REFUND"
                        : "LEGACY-REFUND";
                    var note = root.TryGetProperty("note", out var noteValue) ? noteValue.GetString() : null;
                    var merchantId = root.TryGetProperty("confirmedByMerchantId", out var merchantValue) &&
                                     merchantValue.TryGetInt64(out var parsedMerchant) ? parsedMerchant : 0L;
                    var confirmedAt = root.TryGetProperty("confirmedAt", out var dateValue) &&
                                      dateValue.TryGetDateTime(out var parsedDate) ? parsedDate : DateTime.MinValue;
                    entries.Add(new SellerRefundHistoryEntry(legacyAmount, reference, note, merchantId, confirmedAt));
                }
            }
            catch (JsonException)
            {
                // Existing non-JSON resolution notes are not refund records.
            }
        }

        var refunded = entries.Sum(x => x.RefundAmountEtb);
        var remaining = Math.Max(0m, orderTotalEtb - refunded);
        var refundStatus = refunded >= orderTotalEtb && orderTotalEtb > 0m
            ? "FULLY_REFUNDED"
            : refunded > 0m
                ? "PARTIALLY_REFUNDED"
                : status == ReturnStatus.REFUNDED ? "FULLY_REFUNDED" : "NOT_REFUNDED";
        return new SellerRefundSummary(refundStatus, refunded, remaining, entries);
    }

    public static string Serialize(IReadOnlyList<SellerRefundHistoryEntry> entries, decimal orderTotalEtb)
    {
        var refunded = entries.Sum(x => x.RefundAmountEtb);
        return JsonSerializer.Serialize(new
        {
            refundStatus = refunded >= orderTotalEtb ? "FULLY_REFUNDED" : "PARTIALLY_REFUNDED",
            refundedAmountEtb = refunded,
            remainingRefundEtb = Math.Max(0m, orderTotalEtb - refunded),
            currency = "ETB",
            refundHistory = entries
        });
    }
}
