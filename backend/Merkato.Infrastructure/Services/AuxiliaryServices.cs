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

    public async Task<PaymentTransaction> InitiatePaymentAsync(
        long orderId, string idempotencyKey, decimal amountEtb, PaymentRail rail,
        CancellationToken ct = default)
    {
        if (orderId <= 0) throw new ArgumentOutOfRangeException(nameof(orderId));
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            throw new ArgumentException("Idempotency key must contain 1 to 128 characters.", nameof(idempotencyKey));
        if (amountEtb <= 0m) throw new InvalidOperationException("Payment amount must be positive.");
        if (!Enum.IsDefined(rail)) throw new InvalidOperationException("Unsupported payment rail.");

        var existing = await _db.PaymentTransactions
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, ct);
        if (existing != null)
        {
            if (existing.OrderId != orderId || existing.AmountEtb != amountEtb ||
                existing.PaymentRail != rail)
                throw new InvalidOperationException("Idempotency key was already used for a different payment request.");

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

    public async Task<PaymentTransaction> ProcessWebhookAsync(
        string provider, PaymentWebhookDto dto, string rawPayload, string signature,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(signature))
            throw new UnauthorizedAccessException("Webhook provider and signature are required.");

        var providerKey = provider.Trim().ToLowerInvariant().Replace("-", string.Empty);
        var (expectedRail, secretName) = providerKey switch
        {
            "telebirr" => (PaymentRail.TELEBIRR, "TELEBIRR_WEBHOOK_SECRET"),
            "cbebirr" => (PaymentRail.CBE_BIRR, "CBEBIRR_WEBHOOK_SECRET"),
            _ => throw new InvalidOperationException("Unsupported payment webhook provider.")
        };

        if (dto.AmountEtb <= 0m || !string.Equals(dto.Currency, "ETB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Webhook amount or currency is invalid.");
        if (string.IsNullOrWhiteSpace(dto.OrderNumber) ||
            string.IsNullOrWhiteSpace(dto.RailTransactionId) ||
            string.IsNullOrWhiteSpace(dto.EventId))
            throw new InvalidOperationException("Webhook event, order and provider transaction identifiers are required.");
        if (!new[] { "SUCCESS", "COMPLETED", "PAID" }.Contains(
                dto.Status?.Trim().ToUpperInvariant()))
            throw new InvalidOperationException("Payment provider has not confirmed a successful payment.");

        var secret = Environment.GetEnvironmentVariable(secretName);
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException($"Webhook secret '{secretName}' is not configured.");

        var signatureValue = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature[7..]
            : signature;
        byte[] suppliedSignature;
        try
        {
            suppliedSignature = Convert.FromHexString(signatureValue);
        }
        catch (FormatException)
        {
            throw new UnauthorizedAccessException("Invalid webhook signature format.");
        }

        var expectedSignature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawPayload));
        if (suppliedSignature.Length != expectedSignature.Length ||
            !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
            throw new UnauthorizedAccessException("Invalid webhook signature.");

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.OrderNumber == dto.OrderNumber, ct)
            ?? throw new KeyNotFoundException("Order was not found.");

        var tx = await _db.PaymentTransactions
            .FirstOrDefaultAsync(p => p.OrderId == order.Id, ct)
            ?? throw new KeyNotFoundException("Payment transaction was not found.");

        if (tx.PaymentRail != expectedRail)
            throw new UnauthorizedAccessException("Webhook provider does not match the selected payment rail.");

        if (tx.Status == PaymentStatus.VERIFIED)
        {
            if (string.Equals(tx.RailTransactionId, dto.RailTransactionId, StringComparison.Ordinal))
                return tx;
            throw new InvalidOperationException("This order already has a verified payment.");
        }

        if (tx.Status != PaymentStatus.PENDING && tx.Status != PaymentStatus.INITIATED)
            throw new InvalidOperationException("Payment transaction is not awaiting provider confirmation.");

        if (dto.AmountEtb != tx.AmountEtb)
            throw new InvalidOperationException("Payment amount does not match the pending transaction.");

        var reusedProviderReference = await _db.PaymentTransactions.AnyAsync(
            p => p.Id != tx.Id && p.RailTransactionId == dto.RailTransactionId, ct);
        if (reusedProviderReference)
            throw new InvalidOperationException("Provider transaction reference has already been processed.");

        if (order.EscrowState != EscrowState.AWAITING_PAYMENT)
            throw new InvalidOperationException("Order is not awaiting payment.");

        tx.Status = PaymentStatus.VERIFIED;
        tx.RailTransactionId = dto.RailTransactionId;
        tx.WebhookVerified = true;
        tx.RawPayloadHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload)));
        tx.VerifiedAt = DateTime.UtcNow;

        await _escrow.LockFundsAsync(order.Id, tx.AmountEtb, ct);
        await _audit.LogAsync(null, "PAYMENT_GATEWAY", "PAYMENT_VERIFIED",
            "PAYMENT", tx.Id.ToString(), "SUCCESS", $"Verified {dto.AmountEtb} ETB", ct);

        await _db.SaveChangesAsync(ct);
        return tx;
    }
}

public class KycService : IKycService
{
    // Format validation is not identity verification. Keep this adapter fail-closed
    // until an authenticated Fayda provider, consent flow, callback verification,
    // and provider-issued verification reference are integrated.
    public Task<User> VerifyFaydaAsync(long userId, string faydaIdNumber, CancellationToken ct = default)
    {
        return Task.FromException<User>(new NotSupportedException(
            "Fayda verification is unavailable until an authenticated Fayda provider integration is configured."));
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
