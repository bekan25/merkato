using System.Security.Cryptography;
using Merkato.Api.Services;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Domain.StateMachines;
using Merkato.Infrastructure.Persistence;
using Merkato.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public class MerkatoBackendTests
{
    private MerkatoDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new MerkatoDbContext(options);
    }

    [Fact]
    public async Task LedgerEngine_ThrowsException_OnImbalancedEntries()
    {
        var db = CreateInMemoryDb(nameof(LedgerEngine_ThrowsException_OnImbalancedEntries));
        var ledger = new LedgerEngine(db);

        // Imbalanced entry: 100 debit vs 80 credit
        var entries = new List<(LedgerAccountType, LedgerEntryType, decimal, long?)>
        {
            (LedgerAccountType.BUYER_PAYABLE, LedgerEntryType.DEBIT, 100m, 1L),
            (LedgerAccountType.ESCROW_LIABILITY, LedgerEntryType.CREDIT, 80m, null)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await ledger.RecordTransactionAsync(1L, "TEST", "REF-1", entries);
        });
    }

    [Fact]
    public async Task EscrowStateMachine_PreventsDirectRelease_BeforeDeliveryOrInspection()
    {
        var current = EscrowState.AWAITING_PAYMENT;
        var next = EscrowState.RELEASED;

        Assert.False(EscrowStateMachine.CanTransition(current, next));
        Assert.Throws<InvalidOperationException>(() => EscrowStateMachine.ValidateTransition(current, next));
    }

    [Fact]
    public async Task DeliveryService_LocksOut_After3FailedOtpAttempts()
    {
        var db = CreateInMemoryDb(nameof(DeliveryService_LocksOut_After3FailedOtpAttempts));
        var audit = new AuditLogService(db);
        var escrow = new EscrowService(db, new LedgerEngine(db), audit);
        var inspection = new InspectionService(db, escrow, audit);
        var delivery = new DeliveryService(db, inspection, audit);

        var created = await delivery.CreateDeliveryAsync(101L, "BEU_DELIVERY", 100m, "8899");

        // Attempt 1: Wrong OTP
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            delivery.VerifyOtpAndCompleteHandoverAsync(created.DeliveryId, 1L, "0000"));

        // Attempt 2: Wrong OTP
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            delivery.VerifyOtpAndCompleteHandoverAsync(created.DeliveryId, 1L, "0001"));

        // Attempt 3: Wrong OTP -> Triggers 15-min lockout
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            delivery.VerifyOtpAndCompleteHandoverAsync(created.DeliveryId, 1L, "0002"));

        // Attempt 4: Even with correct OTP, it should fail due to lockout
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            delivery.VerifyOtpAndCompleteHandoverAsync(created.DeliveryId, 1L, "8899"));

        Assert.Contains("locked", ex.Message);
    }

    [Fact]
    public async Task LedgerEngine_Succeeds_OnBalancedEntries()
    {
        var db = CreateInMemoryDb(nameof(LedgerEngine_Succeeds_OnBalancedEntries));
        var ledger = new LedgerEngine(db);

        var entries = new List<(LedgerAccountType, LedgerEntryType, decimal, long?)>
        {
            (LedgerAccountType.BUYER_PAYABLE, LedgerEntryType.DEBIT, 100m, 1L),
            (LedgerAccountType.ESCROW_LIABILITY, LedgerEntryType.CREDIT, 100m, null)
        };

        await ledger.RecordTransactionAsync(1L, "PAYMENT_CAPTURE", "REF-BAL-1", entries);

        var saved = await db.LedgerTransactions.FirstOrDefaultAsync(t => t.ReferenceId == "REF-BAL-1");
        Assert.NotNull(saved);
        Assert.Equal(1L, saved.OrderId);
    }

    [Fact]
    public void DeliveryStateMachine_PreventsIllegalTerminalStateRegression()
    {
        var current = DeliveryStatus.DELIVERED;
        var next = DeliveryStatus.IN_TRANSIT;

        Assert.False(DeliveryStateMachine.CanTransition(current, next));
        Assert.Throws<InvalidOperationException>(() => DeliveryStateMachine.ValidateTransition(current, next));
    }

    [Fact]
    public async Task TrustScoreService_CalculatesExpectedScore_WithinBounds()
    {
        var db = CreateInMemoryDb(nameof(TrustScoreService_CalculatesExpectedScore_WithinBounds));
        var svc = new TrustScoreService(db);

        db.Users.Add(new User
        {
            Id = 42L,
            PhoneNumber = "+251911000042",
            FullName = "Verified Buyer",
            IsFaydaVerified = true,
            TrustScore = 60
        });
        await db.SaveChangesAsync();

        var score = await svc.CalculateUserTrustScoreAsync(42L);

        Assert.InRange(score, 70, 100);
    }

    [Fact]
    public async Task RiskAssessmentService_BlocksHighValueUnverifiedUser()
    {
        var db = CreateInMemoryDb(nameof(RiskAssessmentService_BlocksHighValueUnverifiedUser));
        var risk = new RiskAssessmentService(db);

        var unverifiedUser = new User
        {
            Id = 999L,
            PhoneNumber = "+251911000999",
            FullName = "Test Unverified",
            IsFaydaVerified = false,
            TrustScore = 30
        };
        db.Users.Add(unverifiedUser);
        await db.SaveChangesAsync();

        var decision = await risk.EvaluateOrderRiskAsync(999L, 100001m);
        Assert.Equal(RiskDecision.TRANSACTION_BLOCKED, decision);
    }
    [Fact]
    public void PinHasher_HashesNewPinsAndRejectsIncorrectPins()
    {
        var hash = PinHasher.Hash("123456");

        Assert.StartsWith("v2.600000.", hash);
        Assert.True(PinHasher.Verify("123456", hash));
        Assert.False(PinHasher.Verify("654321", hash));
    }

    [Fact]
    public void PinHasher_ContinuesToVerifyExistingLegacyHashes()
    {
        var salt = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            "1234", salt, 100_000, HashAlgorithmName.SHA256, 32);
        var legacyHash = $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

        Assert.True(PinHasher.Verify("1234", legacyHash));
        Assert.False(PinHasher.Verify("4321", legacyHash));
    }

}
