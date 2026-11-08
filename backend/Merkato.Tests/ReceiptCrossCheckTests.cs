using Merkato.Api.Services;
using Merkato.Domain.Enums;
using Xunit;

namespace Merkato.Tests;

public sealed class ReceiptCrossCheckTests
{
    // Order created 2026-11-03 12:00 UTC = 15:00 East Africa Time.
    private static readonly DateTime OrderCreatedUtc = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NowUtc = new(2026, 11, 3, 13, 0, 0, DateTimeKind.Utc);

    private const string GoodText =
        "Telebirr\nTransaction Number: DK31AB92XZ\nAmount ETB 250.00\nReceiver 2519****5678\n03-11-2026";

    private static ReceiptOcrResult Ocr(
        string status = "MATCHED", string? provider = "Telebirr", decimal? amount = 250m,
        DateTime? date = null, string? text = GoodText) =>
        new(status, provider, "DK31AB92XZ", amount, date, null, text);

    private static ReceiptCrossCheckResult Check(
        ReceiptOcrResult ocr, string? submitted = "DK31AB92XZ",
        PaymentRail rail = PaymentRail.TELEBIRR, string? receiver = "0911225678") =>
        ReceiptCrossCheck.Evaluate(new ReceiptCrossCheckInput(ocr, submitted, OrderCreatedUtc, rail, receiver, NowUtc));

    [Fact]
    public void CleanReceiptRaisesNoFlags()
    {
        // 15:30 EAT = 12:30 UTC is after the order and before now.
        var result = Check(Ocr(date: new DateTime(2026, 11, 3, 15, 30, 0)));
        Assert.True(result.IsClean);
    }

    [Fact]
    public void UnreadableReceiptOnlyReportsOcrUnavailable()
    {
        var result = Check(new ReceiptOcrResult("UNAVAILABLE", null, null, null, null, "No readable text"));
        Assert.Single(result.Flags);
        Assert.Equal(ReceiptCrossCheck.OcrUnavailable, result.Flags[0]);
    }

    [Fact]
    public void TypedReferenceMissingFromImageIsFlagged()
    {
        var result = Check(Ocr(), submitted: "ZZ99ZZ99ZZ");
        Assert.Contains(ReceiptCrossCheck.ReferenceNotInImage, result.Flags);
    }

    [Fact]
    public void TypedReferenceMatchesDespiteSpacingCaseAndLookAlikeCharacters()
    {
        var text = "Transaction Number: DK3I AB92XZ\nAmount ETB 250.00";
        var result = Check(Ocr(text: text, date: new DateTime(2026, 11, 3, 15, 30, 0)), submitted: "dk31ab92xz", receiver: null);
        Assert.DoesNotContain(ReceiptCrossCheck.ReferenceNotInImage, result.Flags);
    }

    [Fact]
    public void NoSubmittedReferenceMeansNoReferenceFlag()
    {
        var result = Check(Ocr(), submitted: null);
        Assert.DoesNotContain(ReceiptCrossCheck.ReferenceNotInImage, result.Flags);
    }

    [Fact]
    public void AmountMismatchAndUnreadAmountAreFlagged()
    {
        Assert.Contains(ReceiptCrossCheck.AmountMismatch, Check(Ocr(status: "MISMATCH", amount: 100m)).Flags);
        Assert.Contains(ReceiptCrossCheck.AmountNotRead, Check(Ocr(status: "NEEDS_REVIEW", amount: null)).Flags);
    }

    [Fact]
    public void ReceiptFromAnotherProviderIsFlagged()
    {
        var result = Check(Ocr(provider: "CBE"), rail: PaymentRail.TELEBIRR);
        Assert.Contains(ReceiptCrossCheck.ProviderMismatch, result.Flags);
    }

    [Fact]
    public void SandboxRailNeverRaisesProviderMismatch()
    {
        var result = Check(Ocr(provider: "CBE"), rail: PaymentRail.SANDBOX);
        Assert.DoesNotContain(ReceiptCrossCheck.ProviderMismatch, result.Flags);
    }

    [Fact]
    public void ReceiptTimeIsReadAsEastAfricaTimeNotUtc()
    {
        var early = Check(Ocr(date: new DateTime(2026, 11, 3, 14, 0, 0)));
        Assert.Contains(ReceiptCrossCheck.ReceiptPredatesOrder, early.Flags);

        var after = Check(Ocr(date: new DateTime(2026, 11, 3, 15, 5, 0)));
        Assert.DoesNotContain(ReceiptCrossCheck.ReceiptPredatesOrder, after.Flags);
        Assert.DoesNotContain(ReceiptCrossCheck.ReceiptDateInFuture, after.Flags);
    }

    [Fact]
    public void ReceiptDatedInTheFutureIsFlagged()
    {
        var result = Check(Ocr(date: new DateTime(2026, 11, 4, 9, 0, 0)));
        Assert.Contains(ReceiptCrossCheck.ReceiptDateInFuture, result.Flags);
    }

    [Fact]
    public void DateWithoutTimeIsComparedByCalendarDay()
    {
        Assert.Contains(ReceiptCrossCheck.ReceiptPredatesOrder, Check(Ocr(date: new DateTime(2026, 11, 2))).Flags);
        Assert.DoesNotContain(ReceiptCrossCheck.ReceiptPredatesOrder, Check(Ocr(date: new DateTime(2026, 11, 3))).Flags);
    }

    [Fact]
    public void SellerPhoneTailIsConfirmedAgainstMaskedReceiver()
    {
        Assert.DoesNotContain(ReceiptCrossCheck.ReceiverNotConfirmed, Check(Ocr(), receiver: "+251911225678").Flags);
        Assert.Contains(ReceiptCrossCheck.ReceiverNotConfirmed, Check(Ocr(), receiver: "0911221111").Flags);
    }

    [Fact]
    public void ShortReceiverIdentifierIsNotCompared()
    {
        Assert.DoesNotContain(ReceiptCrossCheck.ReceiverNotConfirmed, Check(Ocr(), receiver: "123").Flags);
    }

    [Fact]
    public void EveryFlagHasAReadableDescription()
    {
        foreach (var flag in new[]
        {
            ReceiptCrossCheck.OcrUnavailable, ReceiptCrossCheck.ReferenceNotInImage, ReceiptCrossCheck.AmountMismatch,
            ReceiptCrossCheck.AmountNotRead, ReceiptCrossCheck.ProviderMismatch, ReceiptCrossCheck.ReceiptPredatesOrder,
            ReceiptCrossCheck.ReceiptDateInFuture, ReceiptCrossCheck.ReceiverNotConfirmed
        })
            Assert.True(ReceiptCrossCheck.Describe(flag) != flag);
    }

    [Fact]
    public void OcrLanguagesDefaultToEnglishAndAmharicAndRejectUnsafeValues()
    {
        Assert.Equal("eng+amh", ReceiptOcrLanguages.Normalize(null));
        Assert.Equal("eng", ReceiptOcrLanguages.Normalize("eng"));
        Assert.Equal("eng+amh", ReceiptOcrLanguages.Normalize(" eng + amh "));
        Assert.Equal("eng+amh", ReceiptOcrLanguages.Normalize("eng;rm -rf /"));
    }
}
