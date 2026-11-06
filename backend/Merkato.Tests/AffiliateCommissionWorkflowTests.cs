using System.Security.Claims;
using Merkato.Api.Controllers;
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
        Assert.Equal(25, profile.UserId);
        Assert.StartsWith("MK", profile.ReferralCode);
        Assert.Equal(12, profile.ReferralCode.Length);
    }
}
