using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Merkato.Application.DTOs;
using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Merkato.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public sealed class PaymentRetrySafetyTests
{
    private sealed class NoopAudit : IAuditLogService
    {
        public Task LogAsync(long? actorId, string actorRole, string action, string entityType,
            string entityId, string result, string? metadata = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class NoopEscrow : IEscrowService
    {
        public Task<EscrowAccount> LockFundsAsync(long orderId, decimal amountEtb, CancellationToken ct = default) =>
            Task.FromResult(new EscrowAccount { OrderId = orderId, HeldAmountEtb = amountEtb, Status = EscrowState.HELD });
        public Task<EscrowAccount> ReleaseFundsToMerchantAsync(long orderId, long requesterId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<EscrowAccount> RefundFundsToBuyerAsync(long orderId, long requesterId, string reason, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<EscrowAccount> FileDisputeAsync(long orderId, long requesterId, string reason, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static MerkatoDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public async Task NewIdempotencyKeyReusesExistingActivePaymentForSameOrder()
    {
        await using var db = CreateDb();
        db.Orders.Add(new Order
        {
            Id = 10, OrderNumber = "MK-10", BuyerId = 2, SellerId = 3,
            TotalAmountEtb = 500m, PaymentRail = PaymentRail.TELEBIRR,
            PaymentMode = "ESCROW", EscrowState = EscrowState.AWAITING_PAYMENT
        });
        await db.SaveChangesAsync();
        var service = new PaymentService(db, new NoopEscrow(), new NoopAudit());

        var first = await service.InitiatePaymentAsync(10, "checkout-attempt-1", 500m, PaymentRail.TELEBIRR);
        var retry = await service.InitiatePaymentAsync(10, "checkout-attempt-2", 500m, PaymentRail.TELEBIRR);

        Assert.Equal(first.Id, retry.Id);
        Assert.Single(await db.PaymentTransactions.ToListAsync());
    }

    [Fact]
    public async Task PaymentInitiationRejectsRailDifferentFromOrderSnapshot()
    {
        await using var db = CreateDb();
        db.Orders.Add(new Order
        {
            Id = 11, OrderNumber = "MK-11", BuyerId = 2, SellerId = 3,
            TotalAmountEtb = 500m, PaymentRail = PaymentRail.TELEBIRR,
            PaymentMode = "ESCROW", EscrowState = EscrowState.AWAITING_PAYMENT
        });
        await db.SaveChangesAsync();
        var service = new PaymentService(db, new NoopEscrow(), new NoopAudit());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.InitiatePaymentAsync(11, "wrong-rail", 500m, PaymentRail.CBE_BIRR));
        Assert.Empty(await db.PaymentTransactions.ToListAsync());
    }

    [Fact]
    public async Task SuccessfulWebhookSelectsCurrentPendingAttemptAfterOlderAttemptFailed()
    {
        await using var db = CreateDb();
        db.Orders.Add(new Order
        {
            Id = 12, OrderNumber = "MK-12", BuyerId = 2, SellerId = 3,
            TotalAmountEtb = 500m, PaymentRail = PaymentRail.TELEBIRR,
            PaymentMode = "ESCROW", EscrowState = EscrowState.AWAITING_PAYMENT
        });
        var failed = new PaymentTransaction
        {
            Id = 100, OrderId = 12, IdempotencyKey = "old-attempt",
            PaymentRail = PaymentRail.TELEBIRR, AmountEtb = 500m,
            Status = PaymentStatus.FAILED, RailTransactionId = "old-provider-ref",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var pending = new PaymentTransaction
        {
            Id = 101, OrderId = 12, IdempotencyKey = "new-attempt",
            PaymentRail = PaymentRail.TELEBIRR, AmountEtb = 500m,
            Status = PaymentStatus.PENDING, CreatedAt = DateTime.UtcNow
        };
        db.PaymentTransactions.AddRange(failed, pending);
        await db.SaveChangesAsync();

        const string secret = "test-webhook-secret";
        var previousSecret = Environment.GetEnvironmentVariable("TELEBIRR_WEBHOOK_SECRET");
        Environment.SetEnvironmentVariable("TELEBIRR_WEBHOOK_SECRET", secret);
        try
        {
            var dto = new PaymentWebhookDto("event-12", "new-provider-ref", "MK-12", 500m, "ETB", "SUCCESS");
            var raw = JsonSerializer.Serialize(dto);
            var signature = Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(raw)));
            var service = new PaymentService(db, new NoopEscrow(), new NoopAudit());

            var result = await service.ProcessWebhookAsync("telebirr", dto, raw, signature);

            Assert.Equal(pending.Id, result.Id);
            Assert.Equal(PaymentStatus.VERIFIED, pending.Status);
            Assert.Equal("new-provider-ref", pending.RailTransactionId);
            Assert.Equal(PaymentStatus.FAILED, failed.Status);
            Assert.Equal("old-provider-ref", failed.RailTransactionId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TELEBIRR_WEBHOOK_SECRET", previousSecret);
        }
    }
}
