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

        // Search within names so a useful match is still discoverable when the
        // typed word is not the first word in a listing title.
        // Carry the rank through DISTINCT so PostgreSQL can order the selected
        // fields without relying on an unselected ORDER BY expression.
        return ProductLanguageFilter.Apply(
                products.Where(product => product.IsActive),
                language)
            .Where(product => product.Name.ToLower().Contains(term))
            .Select(product => product.Name.Trim())
            .Select(name => new
            {
                Name = name,
                MatchRank = name.ToLower() == term
                    ? 0
                    : name.ToLower().StartsWith(term)
                        ? 1
                        : 2
            })
            .Distinct()
            .OrderBy(match => match.MatchRank)
            .ThenBy(match => match.Name)
            .Select(match => match.Name);
    }
}
