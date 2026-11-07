using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Merkato.Api.Services;

public interface IReceiptOcrService
{
    Task<ReceiptOcrResult> ReadAsync(byte[] image, string extension, decimal expectedAmountEtb, CancellationToken ct);
}

/// <param name="Text">Raw OCR text, kept in memory only so the receipt can be cross-checked; never persisted.</param>
public sealed record ReceiptOcrResult(string Status, string? Provider, string? TransactionReference, decimal? AmountEtb, DateTime? TransactionDate, string? Error, string? Text = null);

/// <summary>
/// Parses receipt text conservatively. OCR output is evidence for manual review only;
/// it must never be treated as proof of settlement.
/// </summary>
public static class ReceiptOcrTextParser
{
    private static readonly string[] DateFormats =
    [
        "MMM dd, yyyy h:mm tt", "MMM d, yyyy h:mm tt",
        "MMMM dd, yyyy h:mm tt", "MMMM d, yyyy h:mm tt",
        "dd MMM yyyy h:mm tt", "d MMM yyyy h:mm tt",
        "dd MMMM yyyy h:mm tt", "d MMMM yyyy h:mm tt",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm",
        "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd HH:mm",
        "yyyy-MM-dd", "yyyy/MM/dd"
    ];

    public static string? ExtractReference(string text)
    {
        // Do not interpret prose such as "Transaction Completed Successfully" as an ID.
        var labelled = Regex.Match(text,
            @"(?im)\b(?:transaction\s*(?:id|no\.?|number|reference)|txn\s*(?:id|no\.?|reference)|reference\s*(?:no\.?|number|id)?|ref\s*(?:no\.?|number)?|receipt\s*(?:no\.?|number|id))\s*[:#\-]?\s*([A-Z0-9][A-Z0-9\-/]{5,149})\b");
        if (labelled.Success)
        {
            var candidate = NormalizeReference(labelled.Groups[1].Value);
            if (IsPlausibleReference(candidate)) return candidate;
        }

        // Provider-style references are safer than arbitrary words after "transaction".
        foreach (Match match in Regex.Matches(text, @"(?i)\b(?:FT|TXN|TRX|CBE|TELE)[A-Z0-9\-/]{5,149}\b"))
        {
            var candidate = NormalizeReference(match.Value);
            if (IsPlausibleReference(candidate)) return candidate;
        }
        return null;
    }

    public static decimal? ExtractAmount(string text, decimal expected)
    {
        var matches = Regex.Matches(text,
            @"(?i)(?:ETB|Birr|Amount|Paid|Total|ድምር)?\s*[: ]*([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]{1,2})?|[0-9]+\.[0-9]{1,2})\s*(?:ETB|Birr)?");
        var values = matches.Cast<Match>()
            .Select(m => m.Groups[1].Value.Replace(",", ""))
            .Select(v => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : (decimal?)null)
            .Where(v => v is > 0 and < 100000000)
            .Select(v => v!.Value).Distinct().ToList();
        return values.Count == 0 ? null : values.OrderBy(v => Math.Abs(v - expected)).First();
    }

    public static DateTime? ExtractDate(string text)
    {
        // Common CBE receipt format: Nov 03, 2026 03:02 PM; also supports ISO.
        var candidates = Regex.Matches(text,
            @"(?i)\b(?:(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+\d{1,2},?\s+20\d{2}(?:\s+\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?)?|\d{1,2}\s+(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+20\d{2}(?:\s+\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?)?|20\d{2}[-/]\d{1,2}[-/]\d{1,2}(?:[ T]\d{1,2}:\d{2}(?::\d{2})?)?)\b");
        foreach (Match match in candidates)
        {
            var value = Regex.Replace(match.Value, @"\s+", " ").Trim();
            if (DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var parsed))
                return parsed;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                return parsed;
        }
        return null;
    }

    public static string? DetectProvider(string text)
    {
        if (Regex.IsMatch(text, "telebirr", RegexOptions.IgnoreCase)) return "Telebirr";
        if (Regex.IsMatch(text, @"\bCBE\b|Commercial Bank of Ethiopia", RegexOptions.IgnoreCase)) return "CBE";
        if (Regex.IsMatch(text, "Awash", RegexOptions.IgnoreCase)) return "Awash";
        if (Regex.IsMatch(text, "Abyssinia", RegexOptions.IgnoreCase)) return "Bank of Abyssinia";
        return null;
    }

    private static string NormalizeReference(string value) =>
        Regex.Replace(value, @"[^A-Z0-9\-/]", "", RegexOptions.IgnoreCase).ToUpperInvariant();

    private static bool IsPlausibleReference(string value) =>
        value.Length is >= 6 and <= 150 &&
        Regex.IsMatch(value, @"[A-Z]") &&
        Regex.IsMatch(value, @"\d") &&
        !Regex.IsMatch(value, @"^(?:COMPLETED|SUCCESSFUL|SUCCESS|TRANSACTION|TRANSFER)$", RegexOptions.IgnoreCase);
}

public static class ReceiptOcrLanguages
{
    public const string Default = "eng+amh";
    public const string Fallback = "eng";

    /// <summary>Accepts values such as "eng" or "eng+amh"; anything else falls back to the default.</summary>
    public static string Normalize(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return Default;
        var parts = configured.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 0 or > 4 || parts.Any(p => !Regex.IsMatch(p, "^[a-z_]{3,12}$")))
            return Default;
        return string.Join('+', parts);
    }
}

public sealed class TesseractReceiptOcrService : IReceiptOcrService
{
    private const int MaxCachedResults = 256;
    private const int MaxTextLength = 8000;
    private static readonly SemaphoreSlim Gate = new(2, 2);
    // Receipts are re-read when admin/seller pending lists load; cache avoids rerunning Tesseract.
    private static readonly ConcurrentDictionary<string, ReceiptOcrResult> Cache = new();
    private readonly string _languages =
        ReceiptOcrLanguages.Normalize(Environment.GetEnvironmentVariable("MERKATO_OCR_LANGUAGES"));

    public async Task<ReceiptOcrResult> ReadAsync(byte[] image, string extension, decimal expectedAmountEtb, CancellationToken ct)
    {
        var cacheKey = $"{Convert.ToHexString(SHA256.HashData(image))}|{expectedAmountEtb}|{_languages}";
        if (Cache.TryGetValue(cacheKey, out var cached)) return cached;

        var temp = Path.Combine(Path.GetTempPath(), $"merkato-{Guid.NewGuid():N}{extension}");
        var entered = false;
        try
        {
            await Gate.WaitAsync(ct);
            entered = true;
            await File.WriteAllBytesAsync(temp, image, ct);
            var run = await RunTesseractAsync(temp, _languages, ct);
            // A missing language pack (for example amh) makes tesseract exit non-zero: retry English-only.
            if (run.ExitCode != 0 && _languages != ReceiptOcrLanguages.Fallback)
                run = await RunTesseractAsync(temp, ReceiptOcrLanguages.Fallback, ct);
            if (run.ExitCode != 0 || string.IsNullOrWhiteSpace(run.Text))
                return Unavailable("No readable text was found on the receipt.");

            var text = run.Text;
            var reference = ReceiptOcrTextParser.ExtractReference(text);
            var amount = ReceiptOcrTextParser.ExtractAmount(text, expectedAmountEtb);
            var date = ReceiptOcrTextParser.ExtractDate(text);
            var provider = ReceiptOcrTextParser.DetectProvider(text);
            var matches = amount.HasValue && Math.Abs(amount.Value - expectedAmountEtb) <= 0.01m;
            var result = new ReceiptOcrResult(
                reference is not null && matches ? "MATCHED" : amount.HasValue && !matches ? "MISMATCH" : "NEEDS_REVIEW",
                provider, reference, amount, date, null,
                text.Length > MaxTextLength ? text[..MaxTextLength] : text);

            if (Cache.Count >= MaxCachedResults) Cache.Clear();
            Cache[cacheKey] = result;
            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Unavailable("Receipt OCR timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Unavailable(ex is System.ComponentModel.Win32Exception
                ? "OCR engine is not installed or unavailable."
                : "Receipt OCR could not be completed.");
        }
        finally
        {
            if (entered) Gate.Release();
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static async Task<(int ExitCode, string Text)> RunTesseractAsync(string path, string languages, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "tesseract",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var arg in new[] { path, "stdout", "-l", languages, "--psm", "6" })
            process.StartInfo.ArgumentList.Add(arg);

        if (!process.Start()) throw new InvalidOperationException("OCR process could not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var text = await output;
            _ = await error;
            return (process.ExitCode, text);
        }
        finally
        {
            // A timed-out run must not leave tesseract running in the background.
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        }
    }

    private static ReceiptOcrResult Unavailable(string error) =>
        new("UNAVAILABLE", null, null, null, null, error);
}
