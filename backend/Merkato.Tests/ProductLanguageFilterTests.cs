using Merkato.Api.Services;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public sealed class ProductLanguageFilterTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData(" EN ", "en")]
    [InlineData("om", "om")]
    [InlineData("AM", "am")]
    public void TryNormalize_AcceptsSupportedLocales(string input, string expected)
    {
        Assert.True(ProductLanguageFilter.TryNormalize(input, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("en-US")]
    [InlineData("english")]
    public void TryNormalize_RejectsUnsupportedLocales(string input)
    {
        Assert.False(ProductLanguageFilter.TryNormalize(input, out var actual));
        Assert.Equal(string.Empty, actual);
    }

    [Fact]
    public async Task Apply_ReturnsOnlyListingsAuthoredInSelectedLanguage()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(Apply_ReturnsOnlyListingsAuthoredInSelectedLanguage))
            .Options;

        await using var db = new MerkatoDbContext(options);
        db.Products.AddRange(
            new Product { Id = 1, SellerId = 10, Name = "Phone", LanguageCode = "en", IsActive = true },
            new Product { Id = 2, SellerId = 10, Name = "Bilbila", LanguageCode = "om", IsActive = true },
            new Product { Id = 3, SellerId = 10, Name = "ስልክ", LanguageCode = "am", IsActive = true },
            new Product { Id = 4, SellerId = 10, Name = "Unpublished", LanguageCode = "om", IsActive = false });

        await db.SaveChangesAsync();

        var results = await ProductLanguageFilter
            .Apply(db.Products.AsNoTracking().Where(product => product.IsActive), "om")
            .Select(product => product.Id)
            .ToListAsync();

        Assert.Equal(new long[] { 2 }, results);
    }
}
