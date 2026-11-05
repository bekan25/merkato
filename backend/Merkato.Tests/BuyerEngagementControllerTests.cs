using System.Security.Claims;
using Merkato.Api.Controllers;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public sealed class BuyerEngagementControllerTests
{
    private static (MerkatoDbContext Db, BuyerEngagementController Controller) Create(string name)
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>().UseInMemoryDatabase(name).Options;
        var db = new MerkatoDbContext(options);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "21"), new Claim(ClaimTypes.Role, "BUYER") }, "test");
        var controller = new BuyerEngagementController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
        };
        return (db, controller);
    }

    [Fact]
    public async Task SaveWishlist_RejectsDuplicateAndScopesByBuyer()
    {
        var (db, controller) = Create(nameof(SaveWishlist_RejectsDuplicateAndScopesByBuyer));
        db.Products.Add(new Product { Id = 7, SellerId = 31, Name = "Phone", PriceEtb = 12000, IsActive = true });
        await db.SaveChangesAsync();

        Assert.IsType<CreatedAtActionResult>(await controller.SaveToWishlist(7, CancellationToken.None));
        Assert.IsType<ConflictObjectResult>(await controller.SaveToWishlist(7, CancellationToken.None));
        Assert.Single(await db.WishlistItems.Where(x => x.BuyerId == 21).ToListAsync());
    }

    [Fact]
    public async Task CreatePriceAlert_RequiresTargetBelowCurrentPriceAndPreventsDuplicate()
    {
        var (db, controller) = Create(nameof(CreatePriceAlert_RequiresTargetBelowCurrentPriceAndPreventsDuplicate));
        db.Products.Add(new Product { Id = 8, SellerId = 31, Name = "Laptop", PriceEtb = 50000, IsActive = true });
        await db.SaveChangesAsync();

        Assert.IsType<BadRequestObjectResult>(await controller.CreatePriceAlert(new PriceAlertInput(8, 50000), CancellationToken.None));
        Assert.IsType<CreatedAtActionResult>(await controller.CreatePriceAlert(new PriceAlertInput(8, 42000), CancellationToken.None));
        Assert.IsType<ConflictObjectResult>(await controller.CreatePriceAlert(new PriceAlertInput(8, 40000), CancellationToken.None));
        Assert.Single(await db.PriceAlerts.Where(x => x.BuyerId == 21 && x.IsActive).ToListAsync());
    }

    [Fact]
    public async Task MarkNotificationRead_DoesNotExposeAnotherBuyersNotification()
    {
        var (db, controller) = Create(nameof(MarkNotificationRead_DoesNotExposeAnotherBuyersNotification));
        db.UserNotifications.Add(new UserNotification { Id = 4, UserId = 99, Title = "Private", Message = "Private message" });
        await db.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await controller.MarkNotificationRead(4, CancellationToken.None));
        Assert.False((await db.UserNotifications.SingleAsync()).IsRead);
    }
}
