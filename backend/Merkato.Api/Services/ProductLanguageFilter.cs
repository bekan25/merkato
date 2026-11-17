namespace Merkato.Api.Services;

using Merkato.Domain.Entities;

/// <summary>
/// Keeps public product discovery scoped to the language in which the seller
/// authored the listing. No automatic translation or cross-language fallback
/// is performed.
/// </summary>
public static class ProductLanguageFilter
{
    private static readonly HashSet<string> SupportedLanguages =
        new(StringComparer.Ordinal) { "en", "om", "am" };

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (SupportedLanguages.Contains(normalized))
            return true;

        normalized = string.Empty;
        return false;
    }

    public static IQueryable<Product> Apply(
        IQueryable<Product> products,
        string normalizedLanguageCode)
    {
        if (!SupportedLanguages.Contains(normalizedLanguageCode))
            throw new ArgumentOutOfRangeException(nameof(normalizedLanguageCode));

        return products.Where(product =>
            product.LanguageCode == normalizedLanguageCode &&
            product.Name != null &&
            product.Name.Trim().Length > 0);
    }
}
