using System.Security.Claims;
using Merkato.Api.Controllers;
using Merkato.Api.Services;
using Merkato.Infrastructure.Services;
using Merkato.Domain.Enums;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public sealed class AffiliateCommissionWorkflowTests
{
    private static ClaimsPrincipal Principal(long userId, string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task AffiliateCanOnlySeeTheirOwnCommissionRowsAndTotals()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(AffiliateCanOnlySeeTheirOwnCommissionRowsAndTotals)).Options;
        await using var db = new MerkatoDbContext(options);
        db.AffiliateCommissions.AddRange(
            new AffiliateCommission { Id = 1, AffiliateUserId = 10, OrderId = 100, EligibleAmountEtb = 1000m, RatePercent = 2m, CommissionEtb = 20m, Status = "HELD" },
            new AffiliateCommission { Id = 2, AffiliateUserId = 10, OrderId = 101, EligibleAmountEtb = 500m, RatePercent = 2m, CommissionEtb = 10m, Status = "AVAILABLE" },
            new AffiliateCommission { Id = 3, AffiliateUserId = 11, OrderId = 102, EligibleAmountEtb = 900m, RatePercent = 2m, CommissionEtb = 18m, Status = "AVAILABLE" });
        await db.SaveChangesAsync();

        var controller = new AffiliateCommissionsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal(10, "BUYER") }
            }
        };

        var result = Assert.IsType<OkObjectResult>(await controller.Mine(CancellationToken.None));
        var value = result.Value!;
        var items = (System.Collections.IEnumerable)value.GetType().GetProperty("items")!.GetValue(value)!;
        Assert.Equal(2, items.Cast<object>().Count());
        var totals = value.GetType().GetProperty("totals")!.GetValue(value)!;
        Assert.Equal(20m, totals.GetType().GetProperty("heldEtb")!.GetValue(totals));
        Assert.Equal(10m, totals.GetType().GetProperty("availableEtb")!.GetValue(totals));
    }

    [Fact]
    public async Task AffiliateProfileEnrollmentIsIdempotent()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(AffiliateProfileEnrollmentIsIdempotent)).Options;
        await using var db = new MerkatoDbContext(options);
        var controller = new AffiliateProfilesController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal(25, "BUYER") }
            }
        };

        var first = await controller.Enroll(CancellationToken.None);
        var second = await controller.Enroll(CancellationToken.None);
        Assert.IsType<CreatedAtActionResult>(first);
        Assert.IsType<OkObjectResult>(second);
        var profile = await db.AffiliateProfiles.SingleAsync();
        Assert.Equal(25L, profile.UserId);
        Assert.StartsWith("MK", profile.ReferralCode);
        Assert.Equal(12, profile.ReferralCode.Length);
    }

    [Fact]
    public async Task FirstVerifiedReferralPurchaseCreatesHeldCommissionAndReferrerCoupon()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(FirstVerifiedReferralPurchaseCreatesHeldCommissionAndReferrerCoupon)).Options;
        await using var db = new MerkatoDbContext(options);
        var order = new Order
        {
            Id = 701, BuyerId = 202, SellerId = 303, ProductId = 404,
            UnitPriceEtb = 500m, Quantity = 1, ProductSubtotalEtb = 500m, DiscountEtb = 50m
        };
        db.AffiliateProfiles.Add(new AffiliateProfile { Id = 501, UserId = 101, ReferralCode = "MKREF101", IsActive = true });
        db.AffiliateReferrals.Add(new AffiliateReferral
        {
            Id = 601, AffiliateUserId = 101, ReferralCode = "MKREF101",
            VisitorKeyHash = new string('D', 64), ReferredUserId = 202,
            ClickedAt = DateTime.UtcNow, RegisteredAt = DateTime.UtcNow
        });
        db.Orders.Add(order);
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 801, OrderId = 701, AmountEtb = 500m, PaymentRail = PaymentRail.TELEBIRR,
            Status = PaymentStatus.VERIFIED, VerifiedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await AffiliateRewardProcessor.RecordVerifiedPurchaseAsync(db, order, null, CancellationToken.None);

        var commission = await db.AffiliateCommissions.SingleAsync();
        Assert.Equal(101L, commission.AffiliateUserId);
        Assert.Equal(450m, commission.EligibleAmountEtb);
        Assert.Equal(9m, commission.CommissionEtb);
        Assert.Equal("HELD", commission.Status);
        Assert.Empty(await db.PromotionCoupons.Where(x => x.SourceReferralId == 601).ToListAsync());

        await AffiliateRewardSettlement.FinalizeAsync(db, order, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal("AVAILABLE", (await db.AffiliateCommissions.SingleAsync()).Status);
        var coupon = await db.PromotionCoupons.SingleAsync(x => x.SourceReferralId == 601);
        Assert.Equal(101L, coupon.AssignedUserId);
        Assert.Equal(50m, coupon.DiscountValue);
        Assert.Equal(200m, coupon.MinimumOrderEtb);
    }

    [Fact]
    public async Task RepeatPurchaseDoesNotCreateAnotherInviteAndEarnCommission()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(RepeatPurchaseDoesNotCreateAnotherInviteAndEarnCommission)).Options;
        await using var db = new MerkatoDbContext(options);
        db.AffiliateProfiles.Add(new AffiliateProfile { Id = 502, UserId = 102, ReferralCode = "MKREF102", IsActive = true });
        db.AffiliateReferrals.Add(new AffiliateReferral
        {
            Id = 602, AffiliateUserId = 102, ReferralCode = "MKREF102",
            VisitorKeyHash = new string('E', 64), ReferredUserId = 203,
            ClickedAt = DateTime.UtcNow, RegisteredAt = DateTime.UtcNow
        });
        db.Orders.AddRange(
            new Order { Id = 702, BuyerId = 203, SellerId = 304, ProductId = 405, UnitPriceEtb = 300m, Quantity = 1, ProductSubtotalEtb = 300m },
            new Order { Id = 703, BuyerId = 203, SellerId = 304, ProductId = 405, UnitPriceEtb = 300m, Quantity = 1, ProductSubtotalEtb = 300m });
        db.PaymentTransactions.AddRange(
            new PaymentTransaction { Id = 802, OrderId = 702, AmountEtb = 300m, PaymentRail = PaymentRail.TELEBIRR, Status = PaymentStatus.VERIFIED },
            new PaymentTransaction { Id = 803, OrderId = 703, AmountEtb = 300m, PaymentRail = PaymentRail.TELEBIRR, Status = PaymentStatus.VERIFIED });
        await db.SaveChangesAsync();

        var secondOrder = await db.Orders.SingleAsync(x => x.Id == 703);
        await AffiliateRewardProcessor.RecordVerifiedPurchaseAsync(db, secondOrder, null, CancellationToken.None);

        Assert.Empty(await db.AffiliateCommissions.ToListAsync());
        Assert.Empty(await db.PromotionCoupons.Where(x => x.SourceReferralId == 602).ToListAsync());
    }
}
