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
        var rows = await db.ReturnRequests.AsNoTracking()
            .Where(x => x.BuyerId == CurrentUserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id, x.OrderId, x.Reason, status = x.Status.ToString(), x.ResolutionNote,
                x.CreatedAt, x.UpdatedAt
            })
            .ToListAsync(ct);
        return Ok(rows);
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
                request.Reason, status = request.Status.ToString(), request.ResolutionNote,
                order.ProductTitle, order.Quantity, order.TotalAmountEtb,
                order.PaymentMode, request.CreatedAt, request.UpdatedAt
            }).ToListAsync(ct);

        return Ok(rows);
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
            return Conflict(new { message = "The returned item must be received before refund confirmation." });

        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == request.OrderId, ct);
        if (order is null) return NotFound(new { message = "The linked order no longer exists." });
        if (order.PaymentMode != "SELLER_DIRECT")
            return Conflict(new { message = "This order uses escrow. Resolve its refund through the existing MERKATO dispute and escrow workflow." });
        if (input.RefundAmountEtb > order.TotalAmountEtb)
            return BadRequest(new { message = "Refund amount cannot exceed the amount paid for the order.", maximumRefundEtb = order.TotalAmountEtb });

        var duplicateReference = await db.PaymentReceipts.AsNoTracking()
            .AnyAsync(x => x.TransactionReference == reference, ct);
        if (duplicateReference)
            return Conflict(new { message = "This transaction reference is already used by a payment receipt." });

        // Reverse the proportional platform commission only when this order had a verified seller-direct accrual.
        // The refund itself remains merchant-reported and is not provider-verified by MERKATO.
        var refundKey = $"seller-direct-commission-refund:{request.Id}";
        if (order.MerchantCommissionEtb > 0m &&
            await db.IdempotencyRecords.AnyAsync(x => x.Key == $"seller-direct-commission:{order.Id}", ct) &&
            !await db.IdempotencyRecords.AnyAsync(x => x.Key == refundKey, ct))
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
            {
                db.LedgerTransactions.Add(SellerCommissionLedger.CreateReversal(order.Id, request.SellerId, reversalAmount, reference));
                db.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    Key = refundKey,
                    RequestHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{request.Id}:{input.RefundAmountEtb}:{reference}"))),
                    ResponseJson = JsonSerializer.Serialize(new { status = "COMMISSION_REVERSED", amountEtb = reversalAmount }),
                    StatusCode = 200,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        request.Status = ReturnStatus.REFUNDED;
        request.ResolutionNote = JsonSerializer.Serialize(new
        {
            refundStatus = "MERCHANT_REPORTED_SENT",
            refundAmountEtb = input.RefundAmountEtb,
            currency = "ETB",
            transactionReference = reference,
            note,
            confirmedByMerchantId = CurrentUserId,
            confirmedAt = DateTime.UtcNow
        });
        request.UpdatedAt = DateTime.UtcNow;
        AddAudit("SELLER_DIRECT_REFUND_REPORTED", "ReturnRequest", request.Id.ToString(),
            new { request.OrderId, request.BuyerId, input.RefundAmountEtb, transactionReference = reference });
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            request.Id,
            request.OrderId,
            status = request.Status.ToString(),
            refundStatus = "MERCHANT_REPORTED_SENT",
            refundAmountEtb = input.RefundAmountEtb,
            currency = "ETB",
            transactionReference = reference,
            verifiedByProvider = false,
            message = "The merchant reported that the refund was sent. MERKATO has recorded the reference but has not independently verified or transferred the funds."
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
