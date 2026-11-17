using Merkato.Api.Services;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public sealed class ProductSearchSuggestionTests
{
    [Fact]
    public async Task Apply_ReturnsOnlyActiveProductNamesInSelectedLanguage()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(Apply_ReturnsOnlyActiveProductNamesInSelectedLanguage))
            .Options;

        await using var db = new MerkatoDbContext(options);
        db.Products.AddRange(
            new Product { Id = 1, SellerId = 10, Name = "Phone", Description = "Bilbila", LanguageCode = "en", IsActive = true },
            new Product { Id = 2, SellerId = 10, Name = "Phone Case", LanguageCode = "en", IsActive = true },
            new Product { Id = 3, SellerId = 10, Name = "Bilbila", LanguageCode = "om", IsActive = true },
            new Product { Id = 4, SellerId = 10, Name = "Bilbila Mataa", LanguageCode = "om", IsActive = true },
            new Product { Id = 5, SellerId = 10, Name = "Bilbila dulloomaa", LanguageCode = "om", IsActive = false },
            new Product { Id = 6, SellerId = 10, Name = "ስልክ", LanguageCode = "am", IsActive = true });

        await db.SaveChangesAsync();

        var suggestions = await ProductSearchSuggestionService
            .Apply(db.Products.AsNoTracking(), "om", " bil ")
            .ToListAsync();

        Assert.Equal(new[] { "Bilbila", "Bilbila Mataa" }, suggestions);
    }

    [Fact]
    public async Task Apply_RanksExactThenPrefixThenContainsMatches()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(Apply_RanksExactThenPrefixThenContainsMatches))
            .Options;

        await using var db = new MerkatoDbContext(options);
        db.Products.AddRange(
            new Product { Id = 1, SellerId = 10, Name = "Smart Bilbila", LanguageCode = "om", IsActive = true },
            new Product { Id = 2, SellerId = 10, Name = "Bilbila Mini", LanguageCode = "om", IsActive = true },
            new Product { Id = 3, SellerId = 10, Name = "Bilbila", LanguageCode = "om", IsActive = true },
            new Product { Id = 4, SellerId = 10, Name = "Bilbila Case", LanguageCode = "om", IsActive = true },
            new Product { Id = 5, SellerId = 10, Name = "Other Product", Description = "Bilbila", LanguageCode = "om", IsActive = true },
            new Product { Id = 6, SellerId = 10, Name = "Phone Bilbila", LanguageCode = "en", IsActive = true });

        await db.SaveChangesAsync();

        var suggestions = await ProductSearchSuggestionService
            .Apply(db.Products.AsNoTracking(), "om", "bilbila")
            .ToListAsync();

        Assert.Equal(
            new[] { "Bilbila", "Bilbila Case", "Bilbila Mini", "Smart Bilbila" },
            suggestions);
    }

    [Fact]
    public async Task Apply_UsesProductNameOnlyAndDoesNotSuggestDescriptionText()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(Apply_UsesProductNameOnlyAndDoesNotSuggestDescriptionText))
            .Options;

        await using var db = new MerkatoDbContext(options);
        db.Products.Add(new Product
        {
            Id = 1,
            SellerId = 10,
            Name = "Phone",
            Description = "Bilbila",
            LanguageCode = "en",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var suggestions = await ProductSearchSuggestionService
            .Apply(db.Products.AsNoTracking(), "en", "bil")
            .ToListAsync();

        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task Apply_ReturnsNoSuggestionsForTermsShorterThanTwoCharacters()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(Apply_ReturnsNoSuggestionsForTermsShorterThanTwoCharacters))
            .Options;

        await using var db = new MerkatoDbContext(options);
        db.Products.Add(new Product { Id = 1, SellerId = 10, Name = "Phone", LanguageCode = "en", IsActive = true });
        await db.SaveChangesAsync();

        var suggestions = await ProductSearchSuggestionService
            .Apply(db.Products.AsNoTracking(), "en", "p")
            .ToListAsync();

        Assert.Empty(suggestions);
    }
}
