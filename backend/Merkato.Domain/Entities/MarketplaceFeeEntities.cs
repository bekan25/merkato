using System.ComponentModel.DataAnnotations.Schema;

namespace Merkato.Domain.Entities;

/// <summary>
/// Singleton marketplace pricing configuration. Rates are percentages, not fractions.
/// The active row is Id=1 and is seeded by the database migration.
/// </summary>
public sealed class MarketplaceFeeSettings
{
    public int Id { get; set; } = 1;

    [Column(TypeName = "numeric(7,4)")]
    public decimal MerchantCommissionRatePercent { get; set; } = 1.0000m;

    [Column(TypeName = "numeric(7,4)")]
    public decimal BuyerServiceFeeRatePercent { get; set; } = 0.5000m;

    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public long? UpdatedByUserId { get; set; }
}
