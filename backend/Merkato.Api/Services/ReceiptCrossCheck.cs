using System.Text.RegularExpressions;
using Merkato.Domain.Enums;

namespace Merkato.Api.Services;

public sealed record ReceiptCrossCheckInput(
    ReceiptOcrResult Ocr,
    string? SubmittedReference,
    DateTime OrderCreatedAtUtc,
    PaymentRail ExpectedRail,
    string? ExpectedReceiverIdentifier,
    DateTime NowUtc);

public sealed record ReceiptCrossCheckResult(IReadOnlyList<string> Flags)
{
    public bool IsClean => Flags.Count == 0;
}

/// <summary>
/// Compares what the receipt image shows with what MERKATO already knows about the order.
/// Flags are advice for the human reviewer. They never approve or reject a payment, and an
/// empty flag list is NOT proof that money moved: only the seller's or administrator's own
/// account statement (or a provider API) can confirm that.
/// </summary>
public static class ReceiptCrossCheck
{
    public const string OcrUnavailable = "OCR_UNAVAILABLE";
    public const string ReferenceNotInImage = "REFERENCE_NOT_IN_IMAGE";
    public const string AmountMismatch = "AMOUNT_MISMATCH";
    public const string AmountNotRead = "AMOUNT_NOT_READ";
    public const string ProviderMismatch = "PROVIDER_MISMATCH";
    public const string ReceiptPredatesOrder = "RECEIPT_PREDATES_ORDER";
    public const string ReceiptDateInFuture = "RECEIPT_DATE_IN_FUTURE";
    public const string ReceiverNotConfirmed = "RECEIVER_NOT_CONFIRMED";

    // Receipts show East Africa Time (UTC+3); orders are stored in UTC.
    private static readonly TimeSpan EastAfricaOffset = TimeSpan.FromHours(3);
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(10);

    public static ReceiptCrossCheckResult Evaluate(ReceiptCrossCheckInput input)
    {
        var flags = new List<string>();
        var ocr = input.Ocr;

        // No text was read at all: nothing else can be compared, so say so and stop.
        if (ocr.Status is "UNAVAILABLE" or "NOT_RUN" || string.IsNullOrWhiteSpace(ocr.Text))
        {
            flags.Add(OcrUnavailable);
            return new ReceiptCrossCheckResult(flags);
        }

        if (!string.IsNullOrWhiteSpace(input.SubmittedReference) &&
            !ContainsReference(ocr.Text, input.SubmittedReference))
            flags.Add(ReferenceNotInImage);

        if (ocr.Status == "MISMATCH") flags.Add(AmountMismatch);
        else if (ocr.AmountEtb is null) flags.Add(AmountNotRead);

        var expectedProvider = ExpectedProvider(input.ExpectedRail);
        if (expectedProvider is not null && ocr.Provider is not null &&
            !string.Equals(expectedProvider, ocr.Provider, StringComparison.OrdinalIgnoreCase))
            flags.Add(ProviderMismatch);

        if (ocr.TransactionDate is { } receiptDate)
        {
            var (predatesOrder, inFuture) = CheckDate(receiptDate, input.OrderCreatedAtUtc, input.NowUtc);
            if (predatesOrder) flags.Add(ReceiptPredatesOrder);
            if (inFuture) flags.Add(ReceiptDateInFuture);
        }

        if (!string.IsNullOrWhiteSpace(input.ExpectedReceiverIdentifier) &&
            !ShowsReceiverTail(ocr.Text, input.ExpectedReceiverIdentifier))
            flags.Add(ReceiverNotConfirmed);

        return new ReceiptCrossCheckResult(flags);
    }

    /// <summary>Human-readable explanation for each flag, for reviewer screens.</summary>
    public static string Describe(string flag) => flag switch
    {
        OcrUnavailable => "The receipt could not be read automatically. Check the image by eye.",
        ReferenceNotInImage => "The transaction ID the buyer typed does not appear in the receipt image.",
        AmountMismatch => "The amount on the receipt does not match the order total.",
        AmountNotRead => "No amount could be read from the receipt.",
        ProviderMismatch => "The receipt looks like it comes from a different provider than the one chosen for this order.",
        ReceiptPredatesOrder => "The receipt is dated before the order was created. It may be an old receipt.",
        ReceiptDateInFuture => "The receipt is dated in the future. The image may have been edited.",
        ReceiverNotConfirmed => "The end of the seller's account or phone number was not found on the receipt. Receipts often mask it, so check manually.",
        _ => flag
    };

    public static bool ContainsReference(string text, string reference)
    {
        var wanted = Fold(reference);
        return wanted.Length > 0 && Fold(text).Contains(wanted, StringComparison.Ordinal);
    }

    // Keep letters and digits only, upper-case, and treat look-alike characters OCR confuses
    // (O/0, I/L/1) as equal so a one-character misread does not hide a genuine match.
    private static string Fold(string value)
    {
        var chars = value.Where(char.IsAsciiLetterOrDigit)
            .Select(c => char.ToUpperInvariant(c) switch { 'O' or 'Q' => '0', 'I' or 'L' => '1', var u => u });
        return new string(chars.ToArray());
    }

    private static string? ExpectedProvider(PaymentRail rail) => rail switch
    {
        PaymentRail.TELEBIRR => "Telebirr",
        PaymentRail.CBE_BIRR => "CBE",
        PaymentRail.AWASH_BIRR => "Awash",
        PaymentRail.BANK_OF_ABYSSINIA => "Bank of Abyssinia",
        _ => null
    };

    private static (bool PredatesOrder, bool InFuture) CheckDate(DateTime receiptLocal, DateTime orderCreatedUtc, DateTime nowUtc)
    {
        // A date with no time of day can only be compared by calendar day.
        if (receiptLocal.TimeOfDay == TimeSpan.Zero)
        {
            var orderDay = (orderCreatedUtc + EastAfricaOffset).Date;
            var today = (nowUtc + EastAfricaOffset).Date;
            return (receiptLocal.Date < orderDay, receiptLocal.Date > today);
        }

        var receiptUtc = receiptLocal - EastAfricaOffset;
        return (receiptUtc < orderCreatedUtc - ClockTolerance, receiptUtc > nowUtc + ClockTolerance);
    }

    // Receipts usually mask the middle of an account or phone number but show the last digits.
    private static bool ShowsReceiverTail(string text, string receiverIdentifier)
    {
        var digits = Regex.Replace(receiverIdentifier, @"\D", "");
        if (digits.Length < 4) return true; // not enough digits to compare; do not raise a flag.
        var tail = digits[^4..];
        return Regex.Matches(text, @"\d{4,}").Any(m => m.Value.EndsWith(tail, StringComparison.Ordinal));
    }
}
