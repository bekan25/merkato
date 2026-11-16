using System.Security.Claims;
using System.Text.Json;
using Merkato.Api.Controllers;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public sealed class OrderTrustJourneyControllerTests
{
    private static MerkatoDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void SetUser(ControllerBase controller, long id, string role)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                    new Claim(ClaimTypes.Role, role)
                }, "test"))
            }
        };
    }

    [Fact]
    public async Task BuyerCanReadOwnJourneyAndTimelineDoesNotCallReceiptPayment()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Id = 20, Role = Merkato.Domain.Enums.UserRole.MERCHANT, FullName = "Local Seller", IsSellerVerified = true });
        db.Orders.Add(new Order { Id = 10, OrderNumber = "MK-10", BuyerId = 1, SellerId = 20, ProductTitle = "Coffee", Quantity = 2, TotalAmountEtb = 500, CreatedAt = DateTime.UtcNow.AddHours(-2) });
        db.PaymentTransactions.Add(new PaymentTransaction { Id = 30, OrderId = 10, Status = Merkato.Domain.Enums.PaymentStatus.PENDING, AmountEtb = 500, CreatedAt = DateTime.UtcNow.AddHours(-1), WebhookVerified = false });
        db.PaymentReceipts.Add(new PaymentReceipt { Id = 40, OrderId = 10, PaymentTransactionId = 30, BuyerId = 1, FileName = "receipt.png", ContentType = "image/png", Content = [1, 2, 3], Sha256 = "abc", Status = "PENDING_REVIEW", CreatedAt = DateTime.UtcNow.AddMinutes(-50) });
        await db.SaveChangesAsync();

        var controller = new OrderTrustJourneyController(db);
        SetUser(controller, 1, "BUYER");

        var result = await controller.GetJourney(10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("ORDER_CREATED", json);
        Assert.Contains("PAYMENT_PENDING", json);
        Assert.Contains("RECEIPT_PENDING_REVIEW", json);
        Assert.Contains("providerCallbackVerified", json);
        Assert.Contains("not proof of payment collection", json);
        Assert.Contains("MERKATO_REVIEWED", json);
    }

    [Fact]
    public async Task UnrelatedBuyerCannotReadAnotherUsersJourney()
    {
        await using var db = CreateDb();
        db.Orders.Add(new Order { Id = 10, OrderNumber = "MK-10", BuyerId = 1, SellerId = 20, ProductTitle = "Coffee" });
        await db.SaveChangesAsync();

        var controller = new OrderTrustJourneyController(db);
        SetUser(controller, 99, "BUYER");

        var result = await controller.GetJourney(10, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task SellerCanReadOwnOrderJourney()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Id = 20, Role = Merkato.Domain.Enums.UserRole.MERCHANT, FullName = "Local Seller" });
        db.Orders.Add(new Order { Id = 10, OrderNumber = "MK-10", BuyerId = 1, SellerId = 20, ProductTitle = "Coffee" });
        await db.SaveChangesAsync();

        var controller = new OrderTrustJourneyController(db);
        SetUser(controller, 20, "MERCHANT");

        var result = await controller.GetJourney(10, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task MissingOrderReturnsNotFound()
    {
        await using var db = CreateDb();
        var controller = new OrderTrustJourneyController(db);
        SetUser(controller, 1, "BUYER");

        var result = await controller.GetJourney(404, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
