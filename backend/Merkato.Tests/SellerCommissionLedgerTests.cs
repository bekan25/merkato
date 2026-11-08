using Merkato.Domain.Enums;
using Xunit;
namespace Merkato.Tests;
public sealed class SellerCommissionLedgerTests
{
    [Fact] public void AccrualCreatesBalancedReceivableAndRevenueEntries()
    {
        var ledger = SellerCommissionLedger.CreateAccrual(42, 7, 125.50m, "TX-ABC");
        Assert.Equal("SELLER_DIRECT_COMMISSION", ledger.TransactionType);
        Assert.Equal(2, ledger.Entries.Count);
        Assert.Equal(125.50m, ledger.Entries.Sum(x => x.AmountEtb));
        Assert.Contains(ledger.Entries, x => x.AccountType == LedgerAccountType.MERCHANT_RECEIVABLE && x.UserId == 7 && x.EntryType == LedgerEntryType.DEBIT);
        Assert.Contains(ledger.Entries, x => x.AccountType == LedgerAccountType.PLATFORM_FEE && x.EntryType == LedgerEntryType.CREDIT);
    }
    [Theory] [InlineData(0)] [InlineData(-1)] public void AccrualRejectsNonPositiveAmounts(decimal amount)
        => Assert.Throws<ArgumentOutOfRangeException>(() => SellerCommissionLedger.CreateAccrual(42, 7, amount, "TX-ABC"));
}
