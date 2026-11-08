using System.Security.Claims;
using Merkato.Api.Controllers;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public sealed class PromotionsControllerTests
{
    private static (MerkatoDbContext Db, PromotionsController Controller) Create(string name, long userId = 21)
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>().UseInMemoryDatabase(name).Options;
        var db = new MerkatoDbContext(options);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, "BUYER") }, "test");
        var controller = new PromotionsController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
        };
        return (db, controller);
    }

    [Fact]
    public async Task Validate_ReturnsDiscountForEligibleBuyer()
    {
        var (db, controller) = Create(nameof(Validate_ReturnsDiscountForEligibleBuyer));
        db.Products.Add(new Product { Id = 5, SellerId = 31, Name = "Headphones", PriceEtb = 1000, IsActive = true });
        db.PromotionCoupons.Add(new PromotionCoupon { Id = 2, Code = "SAVE10", Title = "Save ten percent", DiscountType = "PERCENT", DiscountValue = 10, MinimumOrderEtb = 500, StartsAt = DateTime.UtcNow.AddMinutes(-1), ExpiresAt = DateTime.UtcNow.AddDays(5), PerUserLimit = 1 });
        await db.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(await controller.Validate(new CouponValidationInput("save10", 5), CancellationToken.None));
        var payload = result.Value!;
        Assert.Equal(900m, (decimal)payload.GetType().GetProperty("totalEtb")!.GetValue(payload)!);
        Assert.Equal(100m, (decimal)payload.GetType().GetProperty("discountEtb")!.GetValue(payload)!);
    }

    [Fact]
    public async Task Validate_RejectsRewardCouponAssignedToAnotherBuyer()
    {
        var (db, controller) = Create(nameof(Validate_RejectsRewardCouponAssignedToAnotherBuyer), 21);
        db.Products.Add(new Product { Id = 6, SellerId = 31, Name = "Bag", PriceEtb = 700, IsActive = true });
        db.PromotionCoupons.Add(new PromotionCoupon { Id = 3, Code = "REFERRAL50", Title = "Referral reward", DiscountType = "FIXED", DiscountValue = 50, MinimumOrderEtb = 200, AssignedUserId = 99, StartsAt = DateTime.UtcNow.AddMinutes(-1), ExpiresAt = DateTime.UtcNow.AddDays(5), PerUserLimit = 1 });
        await db.SaveChangesAsync();

        Assert.IsType<ConflictObjectResult>(await controller.Validate(new CouponValidationInput("REFERRAL50", 6), CancellationToken.None));
    }

    [Fact]
    public void CalculateDiscount_RespectsMaximumAndSubtotal()
    {
        var coupon = new PromotionCoupon { DiscountType = "PERCENT", DiscountValue = 30, MaximumDiscountEtb = 100 };
        Assert.Equal(100m, PromotionsController.CalculateDiscount(coupon, 1000m));
        coupon.DiscountType = "FIXED";
        coupon.DiscountValue = 900;
        Assert.Equal(100m, PromotionsController.CalculateDiscount(coupon, 100m));
    }
}
