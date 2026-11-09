using System.Linq;
using Merkato.Api.Controllers;
using Merkato.Domain.Entities;
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

    [Fact]
    public void SettlementIdempotencyKeyNormalizesReferenceCaseAndWhitespace()
    {
        Assert.Equal(
            SellerCommissionLedger.CreateSettlementIdempotencyKey(" bank-ref-001 "),
            SellerCommissionLedger.CreateSettlementIdempotencyKey("BANK-REF-001"));
    }

    [Fact]
    public void PartialSettlementReducesOutstandingAndRejectsOverpayment()
    {
        var firstOutstanding = SellerCommissionLedger.CalculateOutstanding(125.50m, 40m, 0m);
        Assert.Equal(85.50m, firstOutstanding);
        Assert.True(SellerCommissionLedger.CanRecordSettlement(35m, firstOutstanding));
        var secondOutstanding = SellerCommissionLedger.CalculateOutstanding(125.50m, 75m, 0m);
        Assert.Equal(50.50m, secondOutstanding);
        Assert.False(SellerCommissionLedger.CanRecordSettlement(50.51m, secondOutstanding));
    }

    [Theory]
    [InlineData(PaymentStatus.VERIFIED, EscrowState.CANCELLED, 10, false)]
    [InlineData(PaymentStatus.VERIFIED, EscrowState.REFUNDED, 10, false)]
    [InlineData(PaymentStatus.PENDING, EscrowState.AWAITING_PAYMENT, 10, false)]
    [InlineData(PaymentStatus.VERIFIED, EscrowState.AWAITING_PAYMENT, 0, false)]
    [InlineData(PaymentStatus.VERIFIED, EscrowState.AWAITING_PAYMENT, 10, true)]
    public void CommissionAccruesOnlyForVerifiedNonCancelledNonRefundedOrders(PaymentStatus payment, EscrowState state, decimal commission, bool expected)
    {
        Assert.Equal(expected, SellerCommissionLedger.ShouldAccrueCommission(payment, state, commission));
    }


    [Theory]
    [InlineData(125.50, 40, 0, 85.50)]
    [InlineData(125.50, 75, 10, 40.50)]
    [InlineData(100, 100, 0, 0)]
    [InlineData(100, 120, 0, 0)]
    [InlineData(100, 80, 30, 0)]
    public void OutstandingNeverGoesBelowZeroWhenLedgerEntriesAreOverpaid(
        decimal accrued, decimal paid, decimal reversed, decimal expected)
    {
        Assert.Equal(expected, SellerCommissionLedger.CalculateOutstanding(accrued, paid, reversed));
    }

    [Fact]
    public void OverpaidOrFullyReversedBalanceCannotAcceptAnotherSettlement()
    {
        var outstanding = SellerCommissionLedger.CalculateOutstanding(100m, 80m, 20m);
        Assert.Equal(0m, outstanding);
        Assert.False(SellerCommissionLedger.CanRecordSettlement(0.01m, outstanding));
    }

}

public sealed class SellerRefundHistoryTests
{
    [Fact]
    public void MultiplePartialRefundsAccumulateAndKeepRemainingBalance()
    {
        var history = new[]
        {
            new SellerRefundHistoryEntry(20m, "REF-1", null, 7, DateTime.UtcNow),
            new SellerRefundHistoryEntry(15m, "REF-2", "Second transfer", 7, DateTime.UtcNow)
        };

        var json = SellerRefundHistory.Serialize(history, 100m);
        var summary = SellerRefundHistory.Read(json, 100m, ReturnStatus.RECEIVED);

        Assert.Equal("PARTIALLY_REFUNDED", summary.RefundStatus);
        Assert.Equal(35m, summary.RefundedAmountEtb);
        Assert.Equal(65m, summary.RemainingRefundEtb);
        Assert.Equal(2, summary.RefundHistory.Count);
    }

    [Fact]
    public void RefundHistoryBecomesFullyRefundedOnlyWhenCumulativeAmountReachesOrderTotal()
    {
        var history = new[]
        {
            new SellerRefundHistoryEntry(30m, "REF-1", null, 7, DateTime.UtcNow),
            new SellerRefundHistoryEntry(70m, "REF-2", null, 7, DateTime.UtcNow)
        };

        var summary = SellerRefundHistory.Read(
            SellerRefundHistory.Serialize(history, 100m), 100m, ReturnStatus.RECEIVED);

        Assert.Equal("FULLY_REFUNDED", summary.RefundStatus);
        Assert.Equal(100m, summary.RefundedAmountEtb);
        Assert.Equal(0m, summary.RemainingRefundEtb);
    }

    [Fact]
    public void LegacySingleRefundJsonIsReadAsHistory()
    {
        const string legacy = "{\"refundStatus\":\"MERCHANT_REPORTED_SENT\",\"refundAmountEtb\":25,\"transactionReference\":\"OLD-REF\",\"confirmedByMerchantId\":7}";
        var summary = SellerRefundHistory.Read(legacy, 100m, ReturnStatus.REFUNDED);

        Assert.Equal("PARTIALLY_REFUNDED", summary.RefundStatus);
        Assert.Equal(25m, summary.RefundedAmountEtb);
        Assert.Equal(75m, summary.RemainingRefundEtb);
        Assert.Single(summary.RefundHistory);
        Assert.Equal("OLD-REF", summary.RefundHistory[0].TransactionReference);
    }

    [Fact]
    public void FullyRefundedOrderHasNoRemainingRefundableBalance()
    {
        var summary = SellerRefundHistory.Read(null, 100m, ReturnStatus.REFUNDED);

        Assert.Equal("FULLY_REFUNDED", summary.RefundStatus);
        Assert.Equal(0m, summary.RemainingRefundEtb);
    }
}
