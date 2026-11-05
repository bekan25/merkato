using System.ComponentModel.DataAnnotations.Schema;

namespace Merkato.Domain.Entities;

public sealed class PromotionCoupon
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "PERCENT";
    [Column(TypeName = "numeric(18,4)")]
    public decimal DiscountValue { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal? MaximumDiscountEtb { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal MinimumOrderEtb { get; set; }
    public long? SellerId { get; set; }
    public long? AssignedUserId { get; set; }
    public long? SourceReferralId { get; set; }
    public int? UsageLimit { get; set; }
    public int PerUserLimit { get; set; } = 1;
    public DateTime StartsAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class CouponRedemption
{
    public long Id { get; set; }
    public long CouponId { get; set; }
    public long BuyerId { get; set; }
    public long OrderId { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal DiscountEtb { get; set; }
    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;
}
