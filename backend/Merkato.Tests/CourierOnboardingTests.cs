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

public sealed class CourierOnboardingTests
{
    [Fact]
    public async Task AdminCanCreateDedicatedCourierWithNormalizedPhoneAndHashedPin()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(AdminCanCreateDedicatedCourierWithNormalizedPhoneAndHashedPin))
            .Options;
        await using var db = new MerkatoDbContext(options);
        var controller = new AdminOperationsController(db);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "99"),
            new Claim(ClaimTypes.Role, "ADMIN")
        }, "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var result = await controller.CreateCourier(
            new AdminCourierCreateInput("Test Courier", "0911222333", "123456"),
            CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
        var courier = await db.Users.SingleAsync();
        Assert.Equal("+251911222333", courier.PhoneNumber);
        Assert.Equal(UserRole.COURIER, courier.Role);
        Assert.NotEqual("123456", courier.PinHash);
        Assert.Contains(await db.AuditLogs.ToListAsync(), x => x.Action == "COURIER_CREATED");
    }

    [Fact]
    public async Task AdminCannotCreateCourierWithInvalidEthiopianPhoneOrShortPin()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(AdminCannotCreateCourierWithInvalidEthiopianPhoneOrShortPin))
            .Options;
        await using var db = new MerkatoDbContext(options);
        var controller = new AdminOperationsController(db);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "99"),
            new Claim(ClaimTypes.Role, "ADMIN")
        }, "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var result = await controller.CreateCourier(
            new AdminCourierCreateInput("Test Courier", "12345", "123"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.Users.ToListAsync());
    }
}
