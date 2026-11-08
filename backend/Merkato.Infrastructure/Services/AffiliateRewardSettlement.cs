using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Infrastructure.Services;

/// <summary>
/// Finalizes affiliate rewards only after the order reaches its trusted
/// fulfillment milestone: escrow release or seller-direct delivery OTP.
/// </summary>
public static class AffiliateRewardSettlement
{
    public static async Task FinalizeAsync(
        MerkatoDbContext db,
        Order order,
        CancellationToken ct = default)
    {
        var commission = await db.AffiliateCommissions
            .FirstOrDefaultAsync(x => x.OrderId == order.Id, ct);
        if (commission is null || commission.Status == "REVERSED")
            return;

        if (commission.Status == "HELD")
            commission.Status = "AVAILABLE";

        var referral = await db.AffiliateReferrals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ReferredUserId == order.BuyerId &&
                x.RegisteredAt != null && x.AffiliateUserId != order.BuyerId, ct);
        if (referral is null ||
            !await db.AffiliateProfiles.AnyAsync(x => x.UserId == referral.AffiliateUserId && x.IsActive, ct) ||
            await db.PromotionCoupons.AnyAsync(x => x.SourceReferralId == referral.Id, ct))
            return;

        var now = DateTime.UtcNow;
        db.PromotionCoupons.Add(new PromotionCoupon
        {
            Code = "REFER" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4)),
            Title = "MERKATO referral thank-you reward",
            DiscountType = "FIXED",
            DiscountValue = 50m,
            MinimumOrderEtb = 200m,
            UsageLimit = 1,
            PerUserLimit = 1,
            AssignedUserId = referral.AffiliateUserId,
            SourceReferralId = referral.Id,
            StartsAt = now,
            ExpiresAt = now.AddDays(30),
            CreatedBy = referral.AffiliateUserId
        });
    }
}
