using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Merkato.Api.Controllers;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Xunit;

namespace Merkato.Tests;

public sealed class BuyerReturnEligibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeliveredOrderWithoutVerifiedDeliveryTimestampCannotStartReturn(bool createDelivery)
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(DeliveredOrderWithoutVerifiedDeliveryTimestampCannotStartReturn) + createDelivery)
            .Options;
        await using var db = new MerkatoDbContext(options);
        db.Orders.Add(new Order
        {
            Id = 11, BuyerId = 101, SellerId = 202, OrderNumber = "MK-11",
            ProductTitle = "Test item", DeliveryStatus = DeliveryStatus.DELIVERED
        });
        if (createDelivery)
            db.Deliveries.Add(new Delivery { Id = 11, OrderId = 11, DeliveryId = "DEL-11", Status = DeliveryStatus.DELIVERED });
        await db.SaveChangesAsync();

        var controller = new ReturnsController(db);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "101"),
            new Claim(ClaimTypes.Role, "BUYER")
        }, "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var result = await controller.RequestReturn(new ReturnRequestInput(11, "Item arrived damaged"), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Empty(await db.ReturnRequests.ToListAsync());
    }
}
