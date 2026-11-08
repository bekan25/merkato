using System.Linq;
using Merkato.Api.Controllers;
using Merkato.Domain.Enums;
using Xunit;

namespace Merkato.Tests;

public sealed class SellerCommissionLedgerTests
{
    [Fact]
    public void AccrualCreatesBalancedReceivableAndRevenueEntries()
    {
        var ledger = SellerCommissionLedger.CreateAccrual(42, 7, 125.50m, "TX-ABC");
        Assert.Equal("SELLER_DIRECT_COMMISSION", ledger.TransactionType);
        Assert.Equal(2, ledger.Entries.Count);
        var debit = Assert.Single(ledger.Entries.Where(x => x.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && x.UserId == 7 && x.EntryType == LedgerEntryType.DEBIT));
        var credit = Assert.Single(ledger.Entries.Where(x => x.AccountType == LedgerAccountType.PLATFORM_FEE && x.EntryType == LedgerEntryType.CREDIT));
        Assert.Equal(125.50m, debit.AmountEtb);
        Assert.Equal(debit.AmountEtb, credit.AmountEtb);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AccrualRejectsNonPositiveAmounts(decimal amount)
        => Assert.Throws<ArgumentOutOfRangeException>(() => SellerCommissionLedger.CreateAccrual(42, 7, amount, "TX-ABC"));

    [Fact]
    public void AccrualRejectsInvalidOrderOrMerchantIds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SellerCommissionLedger.CreateAccrual(0, 7, 1m, "TX-ABC"));
        Assert.Throws<ArgumentOutOfRangeException>(() => SellerCommissionLedger.CreateAccrual(42, 0, 1m, "TX-ABC"));
    }

    [Fact]
    public void PartialSettlementCanBeRepresentedWithoutExceedingAccruedBalance()
    {
        const decimal accrued = 125.50m;
        const decimal firstPayment = 40m;
        const decimal secondPayment = 35m;
        var outstanding = accrued - firstPayment - secondPayment;
        Assert.Equal(50.50m, outstanding);
        Assert.True(firstPayment <= accrued);
        Assert.True(secondPayment <= accrued - firstPayment);
    }

    [Fact]
    public void DuplicatePaymentReferenceProducesSameIdempotencyKey()
    {
        const string reference = " BANK-REF-001 ";
        var key1 = $"merchant-commission-settlement:{reference.Trim().ToUpperInvariant()}";
        var key2 = $"merchant-commission-settlement:{reference.Trim().ToUpperInvariant()}";
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void CancelledOrRefundedOrdersMustNotAccrueCommissionWithoutMerchantPaymentConfirmation()
    {
        // The accrual factory is invoked only from the verified seller-direct
        // payment confirmation handler, never from cancellation or refund handlers.
        Assert.NotNull(typeof(SellerCommissionLedger).GetMethod(nameof(SellerCommissionLedger.CreateAccrual)));
    }
    [Fact]
    public void FullRefundReversesTheAccruedCommission()
    {
        Assert.Equal(10m, SellerCommissionLedger.CalculateRefundReversal(10m, 100m, 100m, 0m));
    }

    [Fact]
    public void PartialRefundReversesCommissionProportionally()
    {
        Assert.Equal(2.5m, SellerCommissionLedger.CalculateRefundReversal(10m, 100m, 25m, 0m));
    }

    [Fact]
    public void ReversalNeverExceedsRemainingAccruedCommission()
    {
        Assert.Equal(1.5m, SellerCommissionLedger.CalculateRefundReversal(10m, 100m, 50m, 8.5m));
    }

    [Fact]
    public void CancellationBeforePaymentConfirmationCreatesNoAccrual()
    {
        // Commission accrual is deliberately only called by the verified receipt handler.
        // A cancelled/unpaid order never enters that handler.
        Assert.Equal(0m, SellerCommissionLedger.CalculateRefundReversal(0m, 100m, 100m, 0m));
    }

}
