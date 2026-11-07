using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
namespace Merkato.Api.Services;
public interface IReceiptOcrService
{
    Task<ReceiptOcrResult> ReadAsync(byte[] image, string extension, decimal expectedAmountEtb, CancellationToken ct);
}
public sealed record ReceiptOcrResult(string Status, string? Provider, string? TransactionReference, decimal? AmountEtb, DateTime? TransactionDate, string? Error);
public sealed class TesseractReceiptOcrService : IReceiptOcrService
{
    private static readonly SemaphoreSlim Gate = new(2, 2);
    public async Task<ReceiptOcrResult> ReadAsync(byte[] image, string extension, decimal expectedAmountEtb, CancellationToken ct)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"merkato-{Guid.NewGuid():N}{extension}");
        var entered = false;
        try
        {
            await Gate.WaitAsync(ct); entered = true;
            await File.WriteAllBytesAsync(temp, image, ct);
            using var process = new Process { StartInfo = new ProcessStartInfo { FileName = "tesseract", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
            foreach (var arg in new[] { temp, "stdout", "-l", "eng", "--psm", "6" }) process.StartInfo.ArgumentList.Add(arg);
            if (!process.Start()) return Unavailable("OCR process could not start.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var text = await output; _ = await error;
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(text)) return Unavailable("No readable text was found on the receipt.");
            var reference = ExtractReference(text); var amount = ExtractAmount(text, expectedAmountEtb);
            var date = ExtractDate(text); var provider = DetectProvider(text);
            var matches = amount.HasValue && Math.Abs(amount.Value - expectedAmountEtb) <= 0.01m;
            return new ReceiptOcrResult(reference is not null && matches ? "MATCHED" : amount.HasValue && !matches ? "MISMATCH" : "NEEDS_REVIEW", provider, reference, amount, date, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Unavailable("Receipt OCR timed out."); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex is System.ComponentModel.Win32Exception ? "OCR engine is not installed or unavailable." : "Receipt OCR could not be completed."); }
        finally { if (entered) Gate.Release(); try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
    }
    private static ReceiptOcrResult Unavailable(string error) => new("UNAVAILABLE", null, null, null, null, error);
    private static string? ExtractReference(string text)
    {
        var patterns = new[] { @"(?i)(?:transaction|trans(?:action)?\s*id|txn|reference|ref(?:erence)?\s*(?:no|number)?|receipt\s*(?:no|number))\s*[:#\-]?\s*([A-Z0-9][A-Z0-9\-/]{5,})", @"(?i)\b(?:FT|TXN|TRX|CBE|TELE)[A-Z0-9\-/]{5,}\b" };
        foreach (var pattern in patterns) { var m = Regex.Match(text, pattern); if (!m.Success) continue; var value = m.Groups.Count > 1 && m.Groups[1].Success ? m.Groups[1].Value : m.Value; value = Regex.Replace(value, @"[^A-Z0-9\-/]", "", RegexOptions.IgnoreCase).ToUpperInvariant(); if (value.Length is >= 6 and <= 150) return value; }
        return null;
    }
    private static decimal? ExtractAmount(string text, decimal expected)
    {
        var matches = Regex.Matches(text, @"(?i)(?:ETB|Birr|Amount|Paid|Total|ድምር)?\s*[: ]*([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]{1,2})?|[0-9]+\.[0-9]{1,2})\s*(?:ETB|Birr)?");
        var values = matches.Cast<Match>().Select(m => m.Groups[1].Value.Replace(",", "")).Select(v => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : (decimal?)null).Where(v => v is > 0 and < 100000000).Select(v => v!.Value).Distinct().ToList();
        return values.Count == 0 ? null : values.OrderBy(v => Math.Abs(v - expected)).First();
    }
    private static DateTime? ExtractDate(string text) { var m = Regex.Match(text, @"\b(20\d{2})[-/](\d{1,2})[-/](\d{1,2})(?:[ T](\d{1,2}):(\d{2})(?::(\d{2}))?)?\b"); return m.Success && DateTime.TryParse(m.Value.Replace('/', '-'), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d) ? d : null; }
    private static string? DetectProvider(string text) { if (Regex.IsMatch(text, "telebirr", RegexOptions.IgnoreCase)) return "Telebirr"; if (Regex.IsMatch(text, @"\bCBE\b|Commercial Bank of Ethiopia", RegexOptions.IgnoreCase)) return "CBE"; if (Regex.IsMatch(text, "Awash", RegexOptions.IgnoreCase)) return "Awash"; if (Regex.IsMatch(text, "Abyssinia", RegexOptions.IgnoreCase)) return "Bank of Abyssinia"; return null; }
}