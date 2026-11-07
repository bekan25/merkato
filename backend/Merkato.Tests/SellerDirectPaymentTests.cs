using System.Security.Claims;
using Merkato.Api.Controllers;
using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Merkato.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
}
