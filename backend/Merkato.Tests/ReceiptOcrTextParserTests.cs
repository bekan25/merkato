using Merkato.Api.Services;
using Xunit;

namespace Merkato.Tests;

public sealed class ReceiptOcrTextParserTests
{
    [Fact]
    public void ExtractReference_DoesNotTreatSuccessHeadlineAsTransactionId()
    {
        const string text = "Transaction Completed Successfully!\nTransaction ID: FT263072PM91";
        Assert.Equal("FT263072PM91", ReceiptOcrTextParser.ExtractReference(text));
    }

    [Fact]
    public void ExtractReference_ReturnsNullWhenReceiptHasNoReference()
    {
        const string text = "Transaction Completed Successfully! Transfer successful.";
        Assert.Null(ReceiptOcrTextParser.ExtractReference(text));
    }

    [Fact]
    public void ExtractDate_ParsesCbeEnglishMonthAndTwelveHourTime()
    {
        const string text = "Nov 03, 2026 03:02 PM";
        Assert.Equal(new DateTime(2026, 11, 3, 15, 2, 0), ReceiptOcrTextParser.ExtractDate(text));
    }

    [Fact]
    public void ExtractDate_ParsesIsoDateTime()
    {
        const string text = "2026-11-03 15:02:00";
        Assert.Equal(new DateTime(2026, 11, 3, 15, 2, 0), ReceiptOcrTextParser.ExtractDate(text));
    }

    [Fact]
    public void ExtractAmount_SelectsTransferAmountClosestToExpectedNotTotalDebit()
    {
        const string text = "ETB 6,000.00 Total Amount Debited: ETB6002.40";
        Assert.Equal(6000.00m, ReceiptOcrTextParser.ExtractAmount(text, 6000.00m));
    }

    [Fact]
    public void DetectProvider_RecognizesCommercialBankOfEthiopia()
    {
        Assert.Equal("CBE", ReceiptOcrTextParser.DetectProvider("Commercial Bank of Ethiopia"));
    }
}
