using System.Security.Claims;
using Merkato.Api.Controllers;
using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public sealed class SellerPaymentDestinationSnapshotTests
{
    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task LogAsync(long? actorId, string actorRole, string action, string entityType, string entityId,
            string result, string? metadata = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (MerkatoDbContext Db, SellerPaymentAccountsController Controller) Create(string name)
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>().UseInMemoryDatabase(name).Options;
        var db = new MerkatoDbContext(options);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "21"),
            new Claim(ClaimTypes.Role, "BUYER")
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

    [Fact]
    public async Task BuyerGetsOriginalOrderDestinationAfterSellerEditsCurrentAccount()
    {
        var (db, controller) = Create(nameof(BuyerGetsOriginalOrderDestinationAfterSellerEditsCurrentAccount));
        db.Orders.Add(new Order
        {
            Id = 701, OrderNumber = "MK-SNAPSHOT-701", SellerId = 31, BuyerId = 21,
            PaymentRail = PaymentRail.TELEBIRR, PaymentMode = "SELLER_DIRECT",
            TotalAmountEtb = 500m,
            SellerPaymentAccountHolderName = "Original Seller",
            SellerPaymentAccountIdentifier = "TEST-OLD-ACCOUNT",
            SellerPaymentInstructions = "Original instructions",
            SellerPaymentDestinationCapturedAt = DateTime.UtcNow.AddDays(-1)
        });
        db.SellerPaymentAccounts.Add(new SellerPaymentAccount
        {
            Id = 702, SellerId = 31, PaymentRail = PaymentRail.TELEBIRR,
            AccountHolderName = "Updated Seller", AccountIdentifier = "TEST-NEW-ACCOUNT",
            Instructions = "Updated instructions", IsActive = true
        });
        await db.SaveChangesAsync();

        var result = await controller.ForOrder(701, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var json = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(ok.Value));
        var destination = json.RootElement.GetProperty("paymentDestination");
        Assert.Equal("Original Seller", destination.GetProperty("accountHolderName").GetString());
        Assert.Equal("TEST-OLD-ACCOUNT", destination.GetProperty("accountIdentifier").GetString());
        Assert.Equal("Original instructions", destination.GetProperty("instructions").GetString());
    }

    [Fact]
    public async Task BuyerIsBlockedWhenOrderHasNoCapturedDestination()
    {
        var (db, controller) = Create(nameof(BuyerIsBlockedWhenOrderHasNoCapturedDestination));
        db.Orders.Add(new Order
        {
            Id = 711, OrderNumber = "MK-NO-SNAPSHOT-711", SellerId = 31, BuyerId = 21,
            PaymentRail = PaymentRail.CBE_BIRR, PaymentMode = "SELLER_DIRECT",
            TotalAmountEtb = 700m
        });
        db.SellerPaymentAccounts.Add(new SellerPaymentAccount
        {
            Id = 712, SellerId = 31, PaymentRail = PaymentRail.CBE_BIRR,
            AccountHolderName = "Current Seller", AccountIdentifier = "TEST-CURRENT-ACCOUNT",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var result = await controller.ForOrder(711, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }
}
