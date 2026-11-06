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

public sealed record MarketplaceFeeQuote(
    decimal EligibleSubtotalEtb,
    decimal MerchantCommissionEtb,
    decimal BuyerServiceFeeEtb,
    decimal BuyerPayableEtb);

public static class MarketplaceFeeCalculator
{
    public static MarketplaceFeeQuote Calculate(
        decimal productSubtotalEtb,
        decimal discountEtb,
        decimal deliveryFeeEtb,
        decimal merchantRatePercent,
        decimal buyerRatePercent,
        bool enabled = true)
    {
        if (productSubtotalEtb < 0m || discountEtb < 0m || deliveryFeeEtb < 0m)
            throw new ArgumentOutOfRangeException(nameof(productSubtotalEtb), "Checkout amounts cannot be negative.");
        if (discountEtb > productSubtotalEtb)
            throw new ArgumentOutOfRangeException(nameof(discountEtb), "Discount cannot exceed the product subtotal.");
        if (merchantRatePercent is < 0m or > 10m)
            throw new ArgumentOutOfRangeException(nameof(merchantRatePercent));
        if (buyerRatePercent is < 0m or > 5m)
            throw new ArgumentOutOfRangeException(nameof(buyerRatePercent));

        var eligible = productSubtotalEtb - discountEtb;
        var merchantRate = enabled ? merchantRatePercent : 0m;
        var buyerRate = enabled ? buyerRatePercent : 0m;
        var merchantFee = Math.Round(eligible * merchantRate / 100m, 2, MidpointRounding.AwayFromZero);
        var buyerFee = Math.Round(eligible * buyerRate / 100m, 2, MidpointRounding.AwayFromZero);
        return new MarketplaceFeeQuote(
            eligible,
            merchantFee,
            buyerFee,
            eligible + buyerFee + deliveryFeeEtb);
    }
}
