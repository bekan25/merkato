using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Merkato.Api.Controllers;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Xunit;

namespace Merkato.Tests;

public sealed class CourierDeliveryAuthorizationTests
{
    private static MerkatoDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MerkatoDbContext(options);
    }

    private static CommerceGrowthController CreateController(MerkatoDbContext db, long courierId, string role = "COURIER")
    {
        var controller = new CommerceGrowthController(db, null!);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, courierId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task CourierCannotReadDeliveryAssignedToAnotherCourier()
    {
        await using var db = CreateDb(nameof(CourierCannotReadDeliveryAssignedToAnotherCourier));
        db.Users.Add(new User { Id = 33, Role = UserRole.COURIER, PhoneNumber = "+251911111111", FullName = "Courier A" });
        db.Orders.Add(new Order { Id = 1, BuyerId = 10, SellerId = 20, OrderNumber = "MK-1", ProductTitle = "Phone" });
        db.Deliveries.Add(new Delivery { Id = 1, OrderId = 1, DeliveryId = "DEL-1", RiderPhone = "+251922222222", Status = DeliveryStatus.ASSIGNED_COURIER });
        await db.SaveChangesAsync();

        var result = await CreateController(db, 33).OrderDelivery(1, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task AssignedActiveCourierCanReadOnlyTheirDelivery()
    {
        await using var db = CreateDb(nameof(AssignedActiveCourierCanReadOnlyTheirDelivery));
        db.Users.Add(new User { Id = 33, Role = UserRole.COURIER, PhoneNumber = "+251911111111", FullName = "Courier A" });
        db.Orders.Add(new Order { Id = 1, BuyerId = 10, SellerId = 20, OrderNumber = "MK-1", ProductTitle = "Phone" });
        db.Deliveries.Add(new Delivery { Id = 1, OrderId = 1, DeliveryId = "DEL-1", RiderPhone = "+251911111111", Status = DeliveryStatus.ASSIGNED_COURIER });
        await db.SaveChangesAsync();

        var result = await CreateController(db, 33).OrderDelivery(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SuspendedCourierCannotReadPreviouslyAssignedDelivery()
    {
        await using var db = CreateDb(nameof(SuspendedCourierCannotReadPreviouslyAssignedDelivery));
        db.Users.Add(new User { Id = 33, Role = UserRole.COURIER, PhoneNumber = "+251911111111", FullName = "Courier A", IsSuspended = true });
        db.Orders.Add(new Order { Id = 1, BuyerId = 10, SellerId = 20, OrderNumber = "MK-1", ProductTitle = "Phone" });
        db.Deliveries.Add(new Delivery { Id = 1, OrderId = 1, DeliveryId = "DEL-1", RiderPhone = "+251911111111", Status = DeliveryStatus.ASSIGNED_COURIER });
        await db.SaveChangesAsync();

        var result = await CreateController(db, 33).OrderDelivery(1, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }
}
