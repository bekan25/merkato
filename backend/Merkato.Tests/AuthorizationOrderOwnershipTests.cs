using System.Security.Claims;
using System.Text.Json;
using Merkato.Api.Controllers;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

/// <summary>
/// Controller ownership-boundary tests against EF persistence and authenticated claims.
/// These are not HTTP-host integration tests; those are added separately with the API host.
/// </summary>
public sealed class AuthorizationOrderOwnershipTests
{
    private static MerkatoDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MerkatoDbContext(options);
    }

    private static void Authenticate(ControllerBase controller, long userId, string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "ownership-test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task BuyerOrderListContainsOnlyOrdersOwnedByAuthenticatedBuyer()
    {
        await using var db = CreateDb(nameof(BuyerOrderListContainsOnlyOrdersOwnedByAuthenticatedBuyer));
        db.Orders.AddRange(
            new Order { Id = 101, BuyerId = 11, SellerId = 21, OrderNumber = "MK-A", ProductTitle = "Buyer A item" },
            new Order { Id = 102, BuyerId = 12, SellerId = 22, OrderNumber = "MK-B", ProductTitle = "Buyer B item" });
        await db.SaveChangesAsync();

        var controller = new OrdersController(db, null!);
        Authenticate(controller, 11, "BUYER");

        var result = Assert.IsType<OkObjectResult>(await controller.GetMyOrders(CancellationToken.None));
        var json = JsonSerializer.SerializeToDocument(result.Value, ApiJson);
        var ids = json.RootElement.EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();

        Assert.Equal(new long[] { 101 }, ids);
    }

    [Fact]
    public async Task MerchantOrderListContainsOnlyOrdersAssignedToAuthenticatedMerchant()
    {
        await using var db = CreateDb(nameof(MerchantOrderListContainsOnlyOrdersAssignedToAuthenticatedMerchant));
        db.Orders.AddRange(
            new Order { Id = 201, BuyerId = 11, SellerId = 21, OrderNumber = "MK-C", ProductTitle = "Merchant A item" },
            new Order { Id = 202, BuyerId = 12, SellerId = 22, OrderNumber = "MK-D", ProductTitle = "Merchant B item" });
        await db.SaveChangesAsync();

        var controller = new OrdersController(db, null!);
        Authenticate(controller, 21, "MERCHANT");

        var result = Assert.IsType<OkObjectResult>(await controller.GetMerchantOrders(CancellationToken.None));
        var json = JsonSerializer.SerializeToDocument(result.Value, ApiJson);
        var ids = json.RootElement.EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();

        Assert.Equal(new long[] { 201 }, ids);
    }

    [Fact]
    public async Task MerchantCannotChangeAnotherMerchantsReturnDecision()
    {
        await using var db = CreateDb(nameof(MerchantCannotChangeAnotherMerchantsReturnDecision));
        var request = new ReturnRequest
        {
            Id = 301, OrderId = 401, BuyerId = 11, SellerId = 22,
            Reason = "Wrong size", Status = ReturnStatus.REQUESTED
        };
        db.ReturnRequests.Add(request);
        await db.SaveChangesAsync();

        var controller = new ReturnsController(db);
        Authenticate(controller, 21, "MERCHANT");

        var result = await controller.Decide(
            301, new MerchantReturnDecisionInput("APPROVE", "Reviewed"), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        var unchanged = await db.ReturnRequests.SingleAsync(x => x.Id == 301);
        Assert.Equal(ReturnStatus.REQUESTED, unchanged.Status);
        Assert.Null(unchanged.ResolutionNote);
    }

    [Fact]
    public async Task MerchantReturnQueueExcludesOtherMerchantsReturns()
    {
        await using var db = CreateDb(nameof(MerchantReturnQueueExcludesOtherMerchantsReturns));
        db.Orders.AddRange(
            new Order { Id = 501, BuyerId = 11, SellerId = 21, OrderNumber = "MK-E", ProductTitle = "A" },
            new Order { Id = 502, BuyerId = 12, SellerId = 22, OrderNumber = "MK-F", ProductTitle = "B" });
        db.ReturnRequests.AddRange(
            new ReturnRequest { Id = 601, OrderId = 501, BuyerId = 11, SellerId = 21, Reason = "A return" },
            new ReturnRequest { Id = 602, OrderId = 502, BuyerId = 12, SellerId = 22, Reason = "B return" });
        await db.SaveChangesAsync();

        var controller = new ReturnsController(db);
        Authenticate(controller, 21, "MERCHANT");

        var result = Assert.IsType<OkObjectResult>(await controller.SellerQueue(CancellationToken.None));
        var json = JsonSerializer.SerializeToDocument(result.Value, ApiJson);
        var ids = json.RootElement.EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();

        Assert.Equal(new long[] { 601 }, ids);
    }
}
