using System.Security.Claims;
using Merkato.Api.Controllers;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Tests;

public sealed class DeliveryNetworkControllerTests
{
    private static MerkatoDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MerkatoDbContext(options);
    }

    private static void SetUser(ControllerBase controller, long id, string role = "BUYER")
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
    public async Task Apply_CreatesPendingProfileForAuthenticatedUser()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Id = 7, FullName = "Test Courier", PhoneNumber = "+251911000007", PinHash = "test" });
        await db.SaveChangesAsync();
        var controller = new DeliveryNetworkController(db);
        SetUser(controller, 7);

        var result = await controller.Apply(
            new DeliveryPartnerApplication(new[] { "Bole", "Gerji" }, "MOTORCYCLE", "Blue motorcycle", 30m),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var profile = await db.DeliveryPartnerProfiles.SingleAsync(x => x.UserId == 7);
        Assert.Equal("PENDING", profile.ApprovalStatus);
        Assert.Contains("Bole", profile.ServiceAreasJson);
    }

    [Fact]
    public async Task AdminReview_ChangesPendingPartnerToApproved()
    {
        await using var db = CreateDb();
        db.Users.AddRange(
            new User { Id = 1, FullName = "Admin", PhoneNumber = "+251911000001", PinHash = "test" },
            new User { Id = 2, FullName = "Courier", PhoneNumber = "+251911000002", PinHash = "test" });
        db.DeliveryPartnerProfiles.Add(new DeliveryPartnerProfile
        {
            UserId = 2, ApprovalStatus = "PENDING", ServiceAreasJson = "[\"Bole\"]", TransportMode = "MOTORCYCLE"
        });
        await db.SaveChangesAsync();
        var controller = new DeliveryNetworkController(db);
        SetUser(controller, 1, "ADMIN");

        var result = await controller.Review(1, new DeliveryPartnerReview("APPROVED", "Identity checked"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var profile = await db.DeliveryPartnerProfiles.SingleAsync(x => x.UserId == 2);
        Assert.Equal("APPROVED", profile.ApprovalStatus);
        Assert.Equal(1, profile.ReviewerUserId);
        Assert.NotNull(profile.ReviewedAt);
    }

    [Fact]
    public async Task Providers_OnlyReturnsEnabledDeliveryOptions()
    {
        await using var db = CreateDb();
        db.DeliveryProviders.AddRange(
            new DeliveryProvider { Code = "SELLER", DisplayName = "Seller delivery", ProviderKind = "SELLER", IsEnabled = true },
            new DeliveryProvider { Code = "KLIK", DisplayName = "Klik", ProviderKind = "COMPANY", IsEnabled = false });
        await db.SaveChangesAsync();
        var controller = new DeliveryNetworkController(db);

        var result = await controller.GetProviders(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var providers = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value);
        Assert.NotNull(providers);
        Assert.Single((System.Collections.IEnumerable)ok.Value!);
    }
}
