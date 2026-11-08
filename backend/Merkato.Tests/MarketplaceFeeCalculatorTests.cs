using Merkato.Domain.Entities;
using Xunit;

namespace Merkato.Tests;

public sealed class MarketplaceFeeCalculatorTests
{
    [Fact]
    public void LaunchRatesCalculateMerchantAndBuyerFeesSeparately()
    {
        var quote = MarketplaceFeeCalculator.Calculate(10_000m, 0m, 80m, 1m, 0.5m);

        Assert.Equal(10_000m, quote.EligibleSubtotalEtb);
        Assert.Equal(100m, quote.MerchantCommissionEtb);
        Assert.Equal(50m, quote.BuyerServiceFeeEtb);
        Assert.Equal(10_130m, quote.BuyerPayableEtb);
    }

    [Fact]
    public void DiscountReducesBothFeeBasesAndBuyerPaysOnlyBuyerFeeAndDelivery()
    {
        var quote = MarketplaceFeeCalculator.Calculate(10_000m, 500m, 80m, 1m, 0.5m);

        Assert.Equal(9_500m, quote.EligibleSubtotalEtb);
        Assert.Equal(95m, quote.MerchantCommissionEtb);
        Assert.Equal(47.50m, quote.BuyerServiceFeeEtb);
        Assert.Equal(9_627.50m, quote.BuyerPayableEtb);
    }

    [Fact]
    public void DisabledMarketplaceFeesDoNotChargeEitherSide()
    {
        var quote = MarketplaceFeeCalculator.Calculate(10_000m, 0m, 80m, 1m, 0.5m, enabled: false);

        Assert.Equal(0m, quote.MerchantCommissionEtb);
        Assert.Equal(0m, quote.BuyerServiceFeeEtb);
        Assert.Equal(10_080m, quote.BuyerPayableEtb);
    }

    [Fact]
    public void RejectsDiscountLargerThanSubtotal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MarketplaceFeeCalculator.Calculate(100m, 101m, 0m, 1m, 0.5m));
    }
}
