using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/v1/merchant-settlements")]
public sealed class MerchantSettlementController(MerkatoDbContext db) : ControllerBase
{
    [HttpGet("mine")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var sellerId)) return Unauthorized();
        var accruals = await (from tx in db.LedgerTransactions.AsNoTracking()
            join entry in db.LedgerEntries.AsNoTracking() on tx.Id equals entry.LedgerTransactionId
            where tx.TransactionType == "SELLER_DIRECT_COMMISSION" && entry.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && entry.UserId == sellerId && entry.EntryType == LedgerEntryType.DEBIT
            select new { tx.Id, tx.OrderId, tx.ReferenceId, tx.CreatedAt, entry.AmountEtb }).ToListAsync(ct);
        var settlements = await (from tx in db.LedgerTransactions.AsNoTracking()
            join entry in db.LedgerEntries.AsNoTracking() on tx.Id equals entry.LedgerTransactionId
            where tx.TransactionType == "MERCHANT_COMMISSION_SETTLEMENT" && entry.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && entry.UserId == sellerId && entry.EntryType == LedgerEntryType.CREDIT
            select new { tx.Id, tx.ReferenceId, tx.CreatedAt, entry.AmountEtb }).ToListAsync(ct);
        var reversals = await (from tx in db.LedgerTransactions.AsNoTracking()
            join entry in db.LedgerEntries.AsNoTracking() on tx.Id equals entry.LedgerTransactionId
            where tx.TransactionType == "SELLER_DIRECT_COMMISSION_REVERSAL" && entry.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && entry.UserId == sellerId && entry.EntryType == LedgerEntryType.CREDIT
            select new { tx.Id, tx.OrderId, tx.ReferenceId, tx.CreatedAt, entry.AmountEtb }).ToListAsync(ct);
        var due = accruals.Sum(x => x.AmountEtb); var paid = settlements.Sum(x => x.AmountEtb); var reversed = reversals.Sum(x => x.AmountEtb);
        return Ok(new { currency = "ETB", commissionDue = due, reversed, paid, outstanding = due - paid - reversed,
            accrualHistory = accruals.OrderByDescending(x => x.CreatedAt), reversalHistory = reversals.OrderByDescending(x => x.CreatedAt),
            paymentHistory = settlements.OrderByDescending(x => x.CreatedAt) });
    }

    [HttpGet("admin/outstanding")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Outstanding(CancellationToken ct)
    {
        var due = await (from tx in db.LedgerTransactions.AsNoTracking() join e in db.LedgerEntries.AsNoTracking() on tx.Id equals e.LedgerTransactionId
            where tx.TransactionType == "SELLER_DIRECT_COMMISSION" && e.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && e.EntryType == LedgerEntryType.DEBIT && e.UserId != null
            group e by e.UserId into g select new { MerchantId = g.Key!.Value, Amount = g.Sum(x => x.AmountEtb) }).ToListAsync(ct);
        var paid = await (from tx in db.LedgerTransactions.AsNoTracking() join e in db.LedgerEntries.AsNoTracking() on tx.Id equals e.LedgerTransactionId
            where tx.TransactionType == "MERCHANT_COMMISSION_SETTLEMENT" && e.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && e.EntryType == LedgerEntryType.CREDIT && e.UserId != null
            group e by e.UserId into g select new { MerchantId = g.Key!.Value, Amount = g.Sum(x => x.AmountEtb) }).ToDictionaryAsync(x => x.MerchantId, x => x.Amount, ct);
        var reversed = await (from tx in db.LedgerTransactions.AsNoTracking() join e in db.LedgerEntries.AsNoTracking() on tx.Id equals e.LedgerTransactionId
            where tx.TransactionType == "SELLER_DIRECT_COMMISSION_REVERSAL" && e.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && e.EntryType == LedgerEntryType.CREDIT && e.UserId != null
            group e by e.UserId into g select new { MerchantId = g.Key!.Value, Amount = g.Sum(x => x.AmountEtb) }).ToDictionaryAsync(x => x.MerchantId, x => x.Amount, ct);
        var ids = due.Select(x => x.MerchantId).ToArray();
        var users = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.FullName, x.PhoneNumber }).ToDictionaryAsync(x => x.Id, ct);
        return Ok(due.Select(x => new { x.MerchantId, merchant = users.GetValueOrDefault(x.MerchantId), commissionDue = x.Amount,
            paid = paid.GetValueOrDefault(x.MerchantId), reversed = reversed.GetValueOrDefault(x.MerchantId), outstanding = x.Amount - paid.GetValueOrDefault(x.MerchantId) - reversed.GetValueOrDefault(x.MerchantId) }).OrderByDescending(x => x.outstanding));
    }

    [HttpPost("admin/record-payment")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> RecordPayment([FromBody] RecordMerchantCommissionPaymentRequest request, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId)) return Unauthorized();
        var reference = request.PaymentReference?.Trim().ToUpperInvariant();
        if (request.MerchantId <= 0 || request.AmountEtb <= 0m || request.AmountEtb > 100000000m || string.IsNullOrWhiteSpace(reference) || reference.Length > 150 || !request.ConfirmedProviderCredit)
            return BadRequest(new { error = "Merchant, positive ETB amount, payment reference, and explicit confirmation of verified provider credit are required." });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var key = SellerCommissionLedger.CreateSettlementIdempotencyKey(reference);
        if (await db.IdempotencyRecords.AnyAsync(x => x.Key == key, ct)) return Conflict(new { error = "This payment reference has already been recorded." });
        if (!await db.Users.AnyAsync(x => x.Id == request.MerchantId && x.Role == UserRole.MERCHANT, ct)) return NotFound(new { error = "Merchant account was not found." });
        var due = await (from tx in db.LedgerTransactions.AsNoTracking() join e in db.LedgerEntries.AsNoTracking() on tx.Id equals e.LedgerTransactionId
            where tx.TransactionType == "SELLER_DIRECT_COMMISSION" && e.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && e.EntryType == LedgerEntryType.DEBIT && e.UserId == request.MerchantId select (decimal?)e.AmountEtb).SumAsync(ct) ?? 0m;
        var paid = await (from tx in db.LedgerTransactions.AsNoTracking() join e in db.LedgerEntries.AsNoTracking() on tx.Id equals e.LedgerTransactionId
            where tx.TransactionType == "MERCHANT_COMMISSION_SETTLEMENT" && e.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && e.EntryType == LedgerEntryType.CREDIT && e.UserId == request.MerchantId select (decimal?)e.AmountEtb).SumAsync(ct) ?? 0m;
        var reversed = await (from tx in db.LedgerTransactions.AsNoTracking() join e in db.LedgerEntries.AsNoTracking() on tx.Id equals e.LedgerTransactionId
            where tx.TransactionType == "SELLER_DIRECT_COMMISSION_REVERSAL" && e.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && e.EntryType == LedgerEntryType.CREDIT && e.UserId == request.MerchantId
            select (decimal?)e.AmountEtb).SumAsync(ct) ?? 0m;
        var outstanding = SellerCommissionLedger.CalculateOutstanding(due, paid, reversed);
        if (!SellerCommissionLedger.CanRecordSettlement(request.AmountEtb, outstanding)) return Conflict(new { error = $"Payment exceeds outstanding commission ({outstanding:0.00} ETB)." });
        db.LedgerTransactions.Add(new LedgerTransaction { OrderId = 0, TransactionType = "MERCHANT_COMMISSION_SETTLEMENT", ReferenceId = reference,
            Description = $"Admin {adminId} recorded verified merchant commission payment", CreatedAt = DateTime.UtcNow,
            Entries = new List<LedgerEntry> {
                new() { AccountType = LedgerAccountType.PLATFORM_CASH, EntryType = LedgerEntryType.DEBIT, AmountEtb = request.AmountEtb },
                new() { AccountType = LedgerAccountType.MERCHANT_RECEIVABLE, UserId = request.MerchantId, EntryType = LedgerEntryType.CREDIT, AmountEtb = request.AmountEtb }
            }});
        db.IdempotencyRecords.Add(new IdempotencyRecord { Key = key, RequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{request.MerchantId}:{request.AmountEtb}:{reference}"))),
            ResponseJson = JsonSerializer.Serialize(new { status = "RECORDED", request.MerchantId, request.AmountEtb, reference }), StatusCode = 200, CreatedAt = DateTime.UtcNow });
        db.AuditLogs.Add(new AuditLog { ActorId = adminId, ActorRole = "ADMIN", Action = "MERCHANT_COMMISSION_PAYMENT_RECORDED", EntityType = "MERCHANT_SETTLEMENT", EntityId = reference,
            Result = "SUCCESS", MetadataJson = JsonSerializer.Serialize(new { request.MerchantId, request.AmountEtb, reference }), CreatedAt = DateTime.UtcNow });
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            if (await db.IdempotencyRecords.AsNoTracking().AnyAsync(x => x.Key == key, ct))
                return Conflict(new { error = "This payment reference has already been recorded." });
            throw;
        }
        return Ok(new { status = "RECORDED", merchantId = request.MerchantId, amountEtb = request.AmountEtb, paymentReference = reference,
            remainingOutstandingEtb = outstanding - request.AmountEtb, message = "Record only after independently verifying the bank/provider credit." });
    }
}

public sealed record RecordMerchantCommissionPaymentRequest(long MerchantId, decimal AmountEtb, string PaymentReference, bool ConfirmedProviderCredit);

public static class SellerCommissionLedger
{
    public static LedgerTransaction CreateAccrual(long orderId, long merchantId, decimal amountEtb, string paymentReference)
    {
        if (orderId <= 0 || merchantId <= 0 || amountEtb <= 0m) throw new ArgumentOutOfRangeException(nameof(amountEtb));
        return new LedgerTransaction { OrderId = orderId, TransactionType = "SELLER_DIRECT_COMMISSION", ReferenceId = $"ORDER-{orderId}:{paymentReference}",
            Description = "Seller-direct commission accrued after merchant payment confirmation", CreatedAt = DateTime.UtcNow,
            Entries = new List<LedgerEntry> {
                new() { AccountType = LedgerAccountType.MERCHANT_RECEIVABLE, UserId = merchantId, EntryType = LedgerEntryType.DEBIT, AmountEtb = amountEtb },
                new() { AccountType = LedgerAccountType.PLATFORM_FEE, EntryType = LedgerEntryType.CREDIT, AmountEtb = amountEtb }
            }};
    }

    public static string CreateSettlementIdempotencyKey(string paymentReference)
    {
        if (string.IsNullOrWhiteSpace(paymentReference)) throw new ArgumentException("A payment reference is required.", nameof(paymentReference));
        return $"merchant-commission-settlement:{paymentReference.Trim().ToUpperInvariant()}";
    }

    public static decimal CalculateOutstanding(decimal accruedEtb, decimal paidEtb, decimal reversedEtb)
    {
        if (accruedEtb < 0m || paidEtb < 0m || reversedEtb < 0m) throw new ArgumentOutOfRangeException(nameof(accruedEtb));
        return Math.Max(0m, accruedEtb - paidEtb - reversedEtb);
    }

    public static bool CanRecordSettlement(decimal paymentEtb, decimal outstandingEtb)
        => paymentEtb > 0m && outstandingEtb >= 0m && paymentEtb <= outstandingEtb;

    public static bool ShouldAccrueCommission(PaymentStatus paymentStatus, EscrowState escrowState, decimal commissionEtb)
        => paymentStatus == PaymentStatus.VERIFIED && escrowState is not EscrowState.CANCELLED and not EscrowState.REFUNDED && commissionEtb > 0m;

    public static decimal CalculateRefundReversal(decimal accruedCommissionEtb, decimal orderTotalEtb, decimal refundAmountEtb, decimal alreadyReversedEtb)
    {
        if (accruedCommissionEtb < 0m || orderTotalEtb <= 0m || refundAmountEtb <= 0m || alreadyReversedEtb < 0m)
            throw new ArgumentOutOfRangeException(nameof(refundAmountEtb));
        var remaining = Math.Max(0m, accruedCommissionEtb - alreadyReversedEtb);
        var refundableShare = decimal.Round(accruedCommissionEtb * Math.Min(refundAmountEtb, orderTotalEtb) / orderTotalEtb, 4, MidpointRounding.AwayFromZero);
        return Math.Min(remaining, refundableShare);
    }

    public static LedgerTransaction CreateReversal(long orderId, long merchantId, decimal amountEtb, string refundReference)
    {
        if (orderId <= 0 || merchantId <= 0 || amountEtb <= 0m) throw new ArgumentOutOfRangeException(nameof(amountEtb));
        return new LedgerTransaction { OrderId = orderId, TransactionType = "SELLER_DIRECT_COMMISSION_REVERSAL",
            ReferenceId = $"ORDER-{orderId}:REFUND:{refundReference}", Description = "Proportional reversal of seller-direct commission after merchant-reported refund",
            CreatedAt = DateTime.UtcNow, Entries = new List<LedgerEntry> {
                new() { AccountType = LedgerAccountType.PLATFORM_FEE, EntryType = LedgerEntryType.DEBIT, AmountEtb = amountEtb },
                new() { AccountType = LedgerAccountType.MERCHANT_RECEIVABLE, UserId = merchantId, EntryType = LedgerEntryType.CREDIT, AmountEtb = amountEtb }
            }};
    }
}
