using Merkato.Domain.Entities;

namespace Merkato.Api.Services;

/// <summary>
/// Produces autocomplete values from active product names in exactly one
/// seller-selected listing language. Descriptions, locations and sellers are
/// intentionally excluded from suggestions.
/// </summary>
public static class ProductSearchSuggestionService
{
    public static IQueryable<string> Apply(
        IQueryable<Product> products,
        string normalizedLanguageCode,
        string searchTerm)
    {
        if (!ProductLanguageFilter.TryNormalize(normalizedLanguageCode, out var language))
            throw new ArgumentOutOfRangeException(nameof(normalizedLanguageCode));

        var term = (searchTerm ?? string.Empty).Trim().ToLowerInvariant();
        if (term.Length < 2)
            return products.Where(product => false).Select(product => product.Name);

        return ProductLanguageFilter.Apply(
                products.Where(product => product.IsActive),
                language)
            .Where(product => product.Name.ToLower().StartsWith(term))
            .Select(product => product.Name.Trim())
            .Distinct()
            .OrderBy(name => name);
    }
}
