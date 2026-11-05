using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Merkato.Api.Controllers;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public sealed class ReferralRewardTests
{
    [Fact]
    public async Task ClaimReferral_IssuesOneAccountBoundWelcomeCoupon()
    {
        const string visitorKey = "browser-visitor-test-001";
        var options = new DbContextOptionsBuilder<MerkatoDbContext>().UseInMemoryDatabase(nameof(ClaimReferral_IssuesOneAccountBoundWelcomeCoupon)).Options;
        await using var db = new MerkatoDbContext(options);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(visitorKey)));
        db.AffiliateProfiles.Add(new AffiliateProfile { Id = 1, UserId = 11, ReferralCode = "MKREFERRAL", IsActive = true });
        db.AffiliateReferrals.Add(new AffiliateReferral { Id = 9, AffiliateUserId = 11, ReferralCode = "MKREFERRAL", VisitorKeyHash = hash });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "22"), new Claim(ClaimTypes.Role, "BUYER") }, "test");
        var controller = new CommerceGrowthController(db, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
        };

        var result = Assert.IsType<OkObjectResult>(await controller.ClaimReferral("MKREFERRAL", new ReferralClaimInput(visitorKey), CancellationToken.None));
        var reward = await db.PromotionCoupons.SingleAsync(x => x.AssignedUserId == 22);
        Assert.Equal(50m, reward.DiscountValue);
        Assert.Equal(200m, reward.MinimumOrderEtb);
        Assert.Equal(1, reward.UsageLimit);
        Assert.Null(reward.SourceReferralId);
        Assert.NotNull(result.Value);
    }
}
