using System.Security.Claims;
using Merkato.Api.Controllers;
using Merkato.Api.Services;
using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Merkato.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Merkato.Tests;

public sealed class SellerDirectPaymentTests
{
    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task LogAsync(long? actorId, string actorRole, string action, string entityType, string entityId,
            string result, string? metadata = null, CancellationToken ct = default) => Task.CompletedTask;
    }
    private static (MerkatoDbContext Db, SellerPaymentAccountsController Controller) Create(string name, long userId, string role)
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>().UseInMemoryDatabase(name).Options;
        var db = new MerkatoDbContext(options);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "test");
        var controller = new SellerPaymentAccountsController(db, new NoopAuditLogService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
        return (db, controller);
    }

    private sealed class NoopInspectionService : IInspectionService
    {
        public Task<InspectionWindow> StartInspectionAsync(long orderId, CancellationToken ct = default) =>
            Task.FromException<InspectionWindow>(new NotSupportedException());
        public Task<InspectionWindow> AcceptInspectionAsync(long orderId, long buyerId, CancellationToken ct = default) =>
            Task.FromException<InspectionWindow>(new NotSupportedException());
        public Task<int> AutoReleaseExpiredInspectionsAsync(CancellationToken ct = default) =>
            Task.FromResult(0);
    }

    [Fact]
    public async Task SellerDirectDeliveryIsBlockedUntilSellerConfirmsFunds()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(SellerDirectDeliveryIsBlockedUntilSellerConfirmsFunds)).Options;
        await using var db = new MerkatoDbContext(options);
        db.Orders.Add(new Order { Id = 100, SellerId = 22, BuyerId = 33, PaymentMode = "SELLER_DIRECT", TotalAmountEtb = 250m });
        await db.SaveChangesAsync();
        var service = new DeliveryService(db, new NoopInspectionService(), new NoopAuditLogService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateDeliveryAsync(100, "BEU_DELIVERY", 0m, "123456", CancellationToken.None));
        Assert.Empty(await db.Deliveries.ToListAsync());
    }

    [Fact]
    public async Task SellerDirectDeliveryIsAllowedOnlyWithSellerConfirmedReceiptAndVerifiedPayment()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(SellerDirectDeliveryIsAllowedOnlyWithSellerConfirmedReceiptAndVerifiedPayment)).Options;
        await using var db = new MerkatoDbContext(options);
        db.Orders.Add(new Order { Id = 101, SellerId = 22, BuyerId = 33, PaymentMode = "SELLER_DIRECT", TotalAmountEtb = 250m });
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 501, OrderId = 101, AmountEtb = 250m, Status = PaymentStatus.VERIFIED,
            WebhookVerified = false, CreatedAt = DateTime.UtcNow
        });
        db.PaymentReceipts.Add(new PaymentReceipt
        {
            Id = 601, OrderId = 101, BuyerId = 33, PaymentTransactionId = 501,
            FileName = "receipt.jpg", ContentType = "image/jpeg", Content = new byte[] { 1, 2, 3 },
            Sha256 = new string('A', 64), Status = "SELLER_CONFIRMED",
            ReviewedBy = 22, ReviewedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new DeliveryService(db, new NoopInspectionService(), new NoopAuditLogService());

        var delivery = await service.CreateDeliveryAsync(101, "BEU_DELIVERY", 0m, "123456", CancellationToken.None);

        Assert.Equal(DeliveryStatus.CREATED, delivery.Status);
    }

    [Fact]
    public void NewOrderDefaultsToLegacyEscrowMode()
    {
        Assert.Equal("ESCROW", new Order().PaymentMode);
    }

    [Fact]
    public void SellerPaymentAccountModelIsUniquePerSellerAndRail()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseNpgsql("Host=localhost;Database=merkato_model_test;Username=merkato_model_test;Password=not-used")
            .Options;
        using var db = new MerkatoDbContext(options);
        var entity = db.Model.FindEntityType(typeof(SellerPaymentAccount));
        Assert.NotNull(entity);
        Assert.Contains(entity!.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(SellerPaymentAccount.SellerId), nameof(SellerPaymentAccount.PaymentRail) }));
        Assert.Equal("SellerPaymentAccounts", entity.GetTableName());
    }

    [Fact]
    public async Task SellerCanSaveOwnAccountAndBuyerCanOnlyReadTheirOrderDestination()
    {
        var (db, sellerController) = Create(nameof(SellerCanSaveOwnAccountAndBuyerCanOnlyReadTheirOrderDestination), 31, "MERCHANT");
        db.Users.Add(new User { Id = 31, Role = UserRole.MERCHANT, FullName = "Seller" });
        db.Users.Add(new User { Id = 21, Role = UserRole.BUYER, FullName = "Buyer" });
        db.Orders.Add(new Order
        {
            Id = 77, OrderNumber = "MK-TEST-77", SellerId = 31, BuyerId = 21,
            PaymentRail = PaymentRail.TELEBIRR, PaymentMode = "SELLER_DIRECT",
            SellerPaymentAccountHolderName = "Seller Name",
            SellerPaymentAccountIdentifier = "TEST-SELLER-ACCOUNT",
            SellerPaymentDestinationCapturedAt = DateTime.UtcNow,
            InventoryReservationState = InventoryReservationState.RESERVED,
            InventoryReservationExpiresAt = DateTime.UtcNow.AddHours(1),
            TotalAmountEtb = 1250m
        });
        await db.SaveChangesAsync();

        var saved = await sellerController.SaveMine(
            new SaveSellerPaymentAccountRequest(PaymentRail.TELEBIRR, "Seller Name", "0911223344", null, null),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(saved);
        Assert.Single(await db.SellerPaymentAccounts.Where(x => x.SellerId == 31).ToListAsync());

        var (_, buyerController) = Create(nameof(SellerCanSaveOwnAccountAndBuyerCanOnlyReadTheirOrderDestination), 21, "BUYER");
        var destination = await buyerController.ForOrder(77, CancellationToken.None);
        Assert.IsType<OkObjectResult>(destination);

        var otherIdentity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "99"),
            new Claim(ClaimTypes.Role, "BUYER")
        }, "test");
        var otherBuyerController = new SellerPaymentAccountsController(db, new NoopAuditLogService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(otherIdentity) }
            }
        };
        var denied = await otherBuyerController.ForOrder(77, CancellationToken.None);
        Assert.IsType<ForbidResult>(denied);
    }

    [Fact]
    public async Task SellerCannotReadOrDeactivateAnotherSellersAccount()
    {
        var (db, controller) = Create(nameof(SellerCannotReadOrDeactivateAnotherSellersAccount), 31, "MERCHANT");
        db.SellerPaymentAccounts.Add(new SellerPaymentAccount
        {
            Id = 8, SellerId = 99, PaymentRail = PaymentRail.CBE_BIRR,
            AccountHolderName = "Other Seller", AccountIdentifier = "1000000000000"
        });
        await db.SaveChangesAsync();

        var result = await controller.DeactivateMine(8, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
        Assert.True((await db.SellerPaymentAccounts.SingleAsync()).IsActive);
    }

    private sealed class NoopEscrowService : IEscrowService
    {
        public Task<EscrowAccount> LockFundsAsync(long orderId, decimal amountEtb, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EscrowAccount> ReleaseFundsToMerchantAsync(long orderId, long requesterId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EscrowAccount> RefundFundsToBuyerAsync(long orderId, long requesterId, string reason, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EscrowAccount> FileDisputeAsync(long orderId, long requesterId, string reason, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoopReceiptOcrService : IReceiptOcrService
    {
        public Task<ReceiptOcrResult> ReadAsync(byte[] image, string extension, decimal expectedAmountEtb, CancellationToken ct) =>
            Task.FromResult(new ReceiptOcrResult("NOT_RUN", null, null, null, null, null));
    }

    [Fact]
    public async Task SellerDirectReceiptConfirmationRequiresActiveSellerAccountAndVerifiedReference()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(SellerDirectReceiptConfirmationRequiresActiveSellerAccountAndVerifiedReference))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        await using var db = new MerkatoDbContext(options);
        db.Products.Add(new Product { Id = 901, SellerId = 22, Name = "Test Product", PriceEtb = 250m });
        db.Orders.Add(new Order
        {
            Id = 801, SellerId = 22, BuyerId = 33, PaymentMode = "SELLER_DIRECT",
            ProductId = 901, UnitPriceEtb = 250m, Quantity = 1, ProductSubtotalEtb = 250m,
            PaymentRail = PaymentRail.TELEBIRR, TotalAmountEtb = 250m,
            EscrowState = EscrowState.AWAITING_PAYMENT
        });
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 802, OrderId = 801, AmountEtb = 250m, PaymentRail = PaymentRail.TELEBIRR,
            Status = PaymentStatus.PENDING
        });
        db.PaymentReceipts.Add(new PaymentReceipt
        {
            Id = 803, OrderId = 801, BuyerId = 33, PaymentTransactionId = 802,
            FileName = "receipt.jpg", ContentType = "image/jpeg", Content = new byte[] { 1, 2, 3 },
            Sha256 = new string('A', 64), TransactionReference = "TXN123456", Status = "PENDING_REVIEW"
        });
        db.SellerPaymentAccounts.Add(new SellerPaymentAccount
        {
            Id = 804, SellerId = 22, PaymentRail = PaymentRail.TELEBIRR,
            AccountHolderName = "Test Seller", AccountIdentifier = "0911223344", IsActive = true
        });
        db.AffiliateProfiles.Add(new AffiliateProfile
        {
            Id = 805, UserId = 99, ReferralCode = "MKREFERRAL99", IsActive = true
        });
        db.AffiliateReferrals.Add(new AffiliateReferral
        {
            Id = 806, AffiliateUserId = 99, ReferralCode = "MKREFERRAL99",
            VisitorKeyHash = new string('C', 64), ReferredUserId = 33,
            ClickedAt = DateTime.UtcNow, RegisteredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "22"),
            new Claim(ClaimTypes.Role, "MERCHANT")
        }, "test");
        var controller = new PaymentReceiptsController(
            db, new NoopEscrowService(), new NoopAuditLogService(), new NoopReceiptOcrService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };

        var result = await controller.SellerReview(
            803, new ReviewPaymentReceiptRequest("APPROVE", "Verified credit in official account", "TXN123456"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("SELLER_CONFIRMED", (await db.PaymentReceipts.SingleAsync()).Status);
        Assert.Equal(PaymentStatus.VERIFIED, (await db.PaymentTransactions.SingleAsync()).Status);
        Assert.Equal("TXN123456", (await db.PaymentTransactions.SingleAsync()).RailTransactionId);
        Assert.False((await db.PaymentTransactions.SingleAsync()).WebhookVerified);
        var affiliateCommission = await db.AffiliateCommissions.SingleAsync();
        Assert.Equal(99L, affiliateCommission.AffiliateUserId);
        Assert.Equal(806L, affiliateCommission.AffiliateReferralId);
        Assert.Equal(5m, affiliateCommission.CommissionEtb);
        Assert.Equal("HELD", affiliateCommission.Status);
        Assert.Empty(await db.PromotionCoupons.Where(x => x.SourceReferralId == 806).ToListAsync());
    }

    [Fact]
    public async Task SellerDirectReceiptCannotBeConfirmedWithoutActiveMatchingPaymentAccount()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(SellerDirectReceiptCannotBeConfirmedWithoutActiveMatchingPaymentAccount))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        await using var db = new MerkatoDbContext(options);
        db.Orders.Add(new Order
        {
            Id = 811, SellerId = 22, BuyerId = 33, PaymentMode = "SELLER_DIRECT",
            PaymentRail = PaymentRail.CBE_BIRR, TotalAmountEtb = 250m,
            EscrowState = EscrowState.AWAITING_PAYMENT
        });
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 812, OrderId = 811, AmountEtb = 250m, PaymentRail = PaymentRail.CBE_BIRR,
            Status = PaymentStatus.PENDING
        });
        db.PaymentReceipts.Add(new PaymentReceipt
        {
            Id = 813, OrderId = 811, BuyerId = 33, PaymentTransactionId = 812,
            FileName = "receipt.jpg", ContentType = "image/jpeg", Content = new byte[] { 1, 2, 3 },
            Sha256 = new string('B', 64), TransactionReference = "CBE123456", Status = "PENDING_REVIEW"
        });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "22"),
            new Claim(ClaimTypes.Role, "MERCHANT")
        }, "test");
        var controller = new PaymentReceiptsController(
            db, new NoopEscrowService(), new NoopAuditLogService(), new NoopReceiptOcrService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };

        var result = await controller.SellerReview(
            813, new ReviewPaymentReceiptRequest("APPROVE", "Attempt without account", "CBE123456"),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("PENDING_REVIEW", (await db.PaymentReceipts.SingleAsync()).Status);
        Assert.Equal(PaymentStatus.PENDING, (await db.PaymentTransactions.SingleAsync()).Status);
    }

    [Fact]
    public async Task SellerDirectAffiliateCommissionBecomesAvailableOnlyAfterDeliveryOtp()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(SellerDirectAffiliateCommissionBecomesAvailableOnlyAfterDeliveryOtp))
            .Options;
        await using var db = new MerkatoDbContext(options);
        db.Orders.Add(new Order
        {
            Id = 901, SellerId = 22, BuyerId = 33, PaymentMode = "SELLER_DIRECT",
            TotalAmountEtb = 250m, DeliveryStatus = DeliveryStatus.CREATED
        });
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 902, OrderId = 901, AmountEtb = 250m, PaymentRail = PaymentRail.TELEBIRR,
            Status = PaymentStatus.VERIFIED, WebhookVerified = false
        });
        db.PaymentReceipts.Add(new PaymentReceipt
        {
            Id = 903, OrderId = 901, BuyerId = 33, PaymentTransactionId = 902,
            FileName = "receipt.jpg", ContentType = "image/jpeg", Content = new byte[] { 1, 2, 3 },
            Sha256 = new string('F', 64), Status = "SELLER_CONFIRMED", ReviewedBy = 22
        });
        db.AffiliateProfiles.Add(new AffiliateProfile
        {
            Id = 905, UserId = 99, ReferralCode = "MKOTPREF99", IsActive = true
        });
        db.AffiliateReferrals.Add(new AffiliateReferral
        {
            Id = 906, AffiliateUserId = 99, ReferralCode = "MKOTPREF99",
            VisitorKeyHash = new string('G', 64), ReferredUserId = 33,
            ClickedAt = DateTime.UtcNow, RegisteredAt = DateTime.UtcNow
        });
        db.AffiliateCommissions.Add(new AffiliateCommission
        {
            Id = 904, AffiliateUserId = 99, AffiliateReferralId = 906, OrderId = 901, EligibleAmountEtb = 250m,
            RatePercent = 2m, CommissionEtb = 5m, Status = "HELD"
        });
        await db.SaveChangesAsync();

        var deliveryService = new DeliveryService(db, new NoopInspectionService(), new NoopAuditLogService());
        var delivery = await deliveryService.CreateDeliveryAsync(901, "BEU_DELIVERY", 0m, "123456");
        Assert.Equal("HELD", (await db.AffiliateCommissions.SingleAsync()).Status);

        await deliveryService.VerifyOtpAndCompleteHandoverAsync(delivery.DeliveryId, 33, "123456");

        Assert.Equal(DeliveryStatus.DELIVERED, (await db.Orders.SingleAsync()).DeliveryStatus);
        Assert.Equal("AVAILABLE", (await db.AffiliateCommissions.SingleAsync()).Status);
        var referrerReward = await db.PromotionCoupons.SingleAsync(x => x.SourceReferralId == 906);
        Assert.Equal(99L, referrerReward.AssignedUserId);
        Assert.Equal(50m, referrerReward.DiscountValue);
    }
}
