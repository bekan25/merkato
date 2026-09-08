using Merkato.Application.DTOs;
using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Merkato.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly MerkatoDbContext _db;
    private readonly IEscrowService _escrow;
    private readonly IAuditLogService _audit;

    public PaymentService(MerkatoDbContext db, IEscrowService escrow, IAuditLogService audit)
    {
        _db = db;
        _escrow = escrow;
        _audit = audit;
    }

    public async Task<PaymentTransaction> InitiatePaymentAsync(long orderId, string idempotencyKey, decimal amountEtb, PaymentRail rail, CancellationToken ct = default)
    {
        if (orderId <= 0) throw new ArgumentOutOfRangeException(nameof(orderId));
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        if (amountEtb <= 0m) throw new InvalidOperationException("Payment amount must be positive.");

        var existing = await _db.PaymentTransactions.FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, ct);
        if (existing != null)
        {
            return existing;
        }

        var tx = new PaymentTransaction
        {
            OrderId = orderId,
            IdempotencyKey = idempotencyKey,
            PaymentRail = rail,
            AmountEtb = amountEtb,
            Currency = "ETB",
            Status = PaymentStatus.PENDING,
            CreatedAt = DateTime.UtcNow
        };

        _db.PaymentTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);
        return tx;
    }

    public async Task<PaymentTransaction> ProcessWebhookAsync(string provider, PaymentWebhookDto dto, string rawPayload, string signature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("Provider is required.", nameof(provider));
        if (string.IsNullOrWhiteSpace(signature)) throw new UnauthorizedAccessException("Missing webhook signature");
        if (dto.AmountEtb <= 0m) throw new InvalidOperationException("Webhook amount must be positive.");
        if (string.IsNullOrWhiteSpace(dto.Currency) || !dto.Currency.Equals("ETB", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsupported or invalid currency");
        if (string.IsNullOrWhiteSpace(dto.OrderNumber)) throw new ArgumentException("Order number is required.", nameof(dto.OrderNumber));
        if (string.IsNullOrWhiteSpace(dto.RailTransactionId)) throw new ArgumentException("Provider transaction identifier is required.", nameof(dto.RailTransactionId));

        var secret = Environment.GetEnvironmentVariable($"{provider.ToUpperInvariant().Replace('-', '_')}_WEBHOOK_SECRET");
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException($"Webhook secret for provider '{provider}' is not configured.");
        }

        var computedSignature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawPayload)));
        if (!string.Equals(signature, computedSignature, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Invalid webhook HMAC-SHA256 signature");
        }

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderNumber == dto.OrderNumber, ct)
            ?? throw new KeyNotFoundException($"Order {dto.OrderNumber} not found");

        var tx = await _db.PaymentTransactions.FirstOrDefaultAsync(p => p.OrderId == order.Id, ct)
            ?? throw new KeyNotFoundException($"Payment transaction for order {dto.OrderNumber} not found");

        if (tx.Status == PaymentStatus.VERIFIED)
        {
            return tx;
        }

        if (dto.AmountEtb != tx.AmountEtb || !dto.Currency.Equals("ETB", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Payment amount or currency mismatch");
        }

        tx.Status = PaymentStatus.VERIFIED;
        tx.RailTransactionId = dto.RailTransactionId;
        tx.WebhookVerified = true;
        tx.VerifiedAt = DateTime.UtcNow;

        await _escrow.LockFundsAsync(order.Id, tx.AmountEtb, ct);
        await _audit.LogAsync(null, "PAYMENT_GATEWAY", "PAYMENT_VERIFIED", "PAYMENT", tx.Id.ToString(), "SUCCESS", $"Verified {dto.AmountEtb} ETB", ct);

        await _db.SaveChangesAsync(ct);
        return tx;
    }
}

public class KycService : IKycService
{
    private readonly MerkatoDbContext _db;
    private readonly ITrustScoreService _trust;
    private readonly IAuditLogService _audit;

    public KycService(MerkatoDbContext db, ITrustScoreService trust, IAuditLogService audit)
    {
        _db = db;
        _trust = trust;
        _audit = audit;
    }

    public async Task<User> VerifyFaydaAsync(long userId, string faydaIdNumber, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found");

        // Format validation (FAN-XXXX-XXXX-XXX)
        if (!System.Text.RegularExpressions.Regex.IsMatch(faydaIdNumber, @"^FAN-\d{4}-\d{4}-[A-Za-z0-9]{3}$"))
        {
            throw new ArgumentException("Invalid Fayda National ID (FAN) format");
        }

        user.FaydaIdNumber = faydaIdNumber;
        user.IsFaydaVerified = true;
        user.TrustScore = await _trust.CalculateUserTrustScoreAsync(userId, ct);
        user.UpdatedAt = DateTime.UtcNow;

        await _audit.LogAsync(userId, user.Role.ToString(), "FAYDA_KYC_VERIFIED", "USER", userId.ToString(), "SUCCESS", "Verified via Fayda adapter", ct);
        await _db.SaveChangesAsync(ct);

        return user;
    }
}

public class TrustScoreService : ITrustScoreService
{
    private readonly MerkatoDbContext _db;

    public TrustScoreService(MerkatoDbContext db)
    {
        _db = db;
    }

    public async Task<int> CalculateUserTrustScoreAsync(long userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (user == null) return 50;

        int score = 50; // Base score

        // 1. KYC Factor (25%)
        if (user.IsFaydaVerified) score += 25;

        // 2. Order Completion Factor (20%)
        var completedOrders = await _db.Orders.CountAsync(o => (o.BuyerId == userId || o.SellerId == userId) && o.EscrowState == EscrowState.RELEASED, ct);
        score += Math.Min(20, completedOrders * 4);

        // 3. Dispute Record Penalty (-10% per active/lost dispute)
        var disputes = await _db.Disputes.CountAsync(d => d.SellerId == userId, ct);
        score -= (disputes * 10);

        return Math.Clamp(score, 10, 100);
    }
}

public class RiskAssessmentService : IRiskAssessmentService
{
    private readonly MerkatoDbContext _db;

    public RiskAssessmentService(MerkatoDbContext db)
    {
        _db = db;
    }

    public async Task<RiskDecision> EvaluateOrderRiskAsync(long userId, decimal orderAmountEtb, CancellationToken ct = default)
    {
        // Fail-closed rule: High velocity / huge order without KYC = Block / Review
        if (orderAmountEtb > 100000m)
        {
            var user = await _db.Users.FindAsync(new object[] { userId }, ct);
            if (user == null || !user.IsFaydaVerified)
            {
                return RiskDecision.TRANSACTION_BLOCKED;
            }
        }

        var recentOrdersCount = await _db.Orders.CountAsync(o => o.BuyerId == userId && o.CreatedAt >= DateTime.UtcNow.AddMinutes(-5), ct);
        if (recentOrdersCount >= 5)
        {
            return RiskDecision.TRANSACTION_BLOCKED;
        }

        return RiskDecision.ALLOW;
    }
}

public class AuditLogService : IAuditLogService
{
    private readonly MerkatoDbContext _db;

    public AuditLogService(MerkatoDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(long? actorId, string actorRole, string action, string entityType, string entityId, string result, string? metadata = null, CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            ActorId = actorId,
            ActorRole = actorRole,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Result = result,
            MetadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}
