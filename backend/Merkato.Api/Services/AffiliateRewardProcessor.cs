using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Services;

/// <summary>
/// Applies referral rewards only after a payment has been verified by the payment
/// provider or by the seller's explicit bank/mobile-money reconciliation.
/// </summary>
public static class AffiliateRewardProcessor
{
    public static async Task RecordVerifiedPurchaseAsync(
        MerkatoDbContext db,
        Order order,
        IConfiguration? configuration,
        CancellationToken ct = default)
    {
        if (!order.ProductId.HasValue || order.UnitPriceEtb <= 0m || order.Quantity <= 0)
            return;

        var referral = await db.AffiliateReferrals
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ReferredUserId == order.BuyerId &&
                x.RegisteredAt != null && x.AffiliateUserId != order.BuyerId, ct);
        if (referral is null ||
            !await db.AffiliateProfiles.AnyAsync(x => x.UserId == referral.AffiliateUserId && x.IsActive, ct))
            return;

        // Invite & Earn is a first verified purchase reward, not a lifetime
        // commission on every repeat order. Exclude the current order because
        // its payment is already VERIFIED in both supported payment paths.
        var hasEarlierVerifiedPurchase = await (
            from payment in db.PaymentTransactions.AsNoTracking()
            join priorOrder in db.Orders.AsNoTracking() on payment.OrderId equals priorOrder.Id
            where priorOrder.BuyerId == order.BuyerId &&
                  priorOrder.Id != order.Id &&
                  payment.Status == PaymentStatus.VERIFIED
            select payment.Id
        ).AnyAsync(ct);
        if (hasEarlierVerifiedPurchase)
            return;

        var now = DateTime.UtcNow;
        if (!await db.AffiliateCommissions.AnyAsync(x => x.OrderId == order.Id, ct))
        {
            var rate = configuration?.GetValue<decimal?>("Commissions:AffiliateRatePercent") ?? 2m;
            if (rate < 0m || rate > 25m)
                throw new InvalidOperationException("Commissions:AffiliateRatePercent must be between 0 and 25 percent.");

            var eligibleAmount = Math.Max(0m, order.ProductSubtotalEtb - order.DiscountEtb);
            db.AffiliateCommissions.Add(new AffiliateCommission
            {
                AffiliateUserId = referral.AffiliateUserId,
                OrderId = order.Id,
                EligibleAmountEtb = eligibleAmount,
                RatePercent = rate,
                CommissionEtb = Math.Round(eligibleAmount * rate / 100m, 2, MidpointRounding.AwayFromZero),
                Status = "HELD",
                CreatedAt = now
            });
        }

        // The referrer receives one 50 ETB coupon after the referred buyer's
        // first verified purchase. SourceReferralId is protected by a unique index.
        if (!await db.PromotionCoupons.AnyAsync(x => x.SourceReferralId == referral.Id, ct))
        {
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

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent webhook/review won the unique OrderId or SourceReferralId
            // race. The verified payment remains valid; the next retry can reconcile.
            foreach (var entry in db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added &&
                (x.Entity is AffiliateCommission commission && commission.OrderId == order.Id ||
                 x.Entity is PromotionCoupon coupon && coupon.SourceReferralId == referral.Id)).ToList())
                entry.State = EntityState.Detached;
        }
    }

    public static Task ReleaseSellerDirectCommissionAsync(
        MerkatoDbContext db,
        long orderId,
        CancellationToken ct = default) =>
        db.AffiliateCommissions
            .Where(x => x.OrderId == orderId && x.Status == "HELD")
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "AVAILABLE"), ct);
}
