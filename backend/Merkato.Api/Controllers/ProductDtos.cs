using Merkato.Domain.Entities;

namespace Merkato.Api.Controllers;

public record CreateProductRequest(
    long SellerId,
    string Name,
    decimal PriceEtb,
    string Location,
    string Category,
    string Emoji,
    int StockQuantity = 0,
    string? Description = null,
    string LanguageCode = "en"
);

public record ProductResponse(
    long Id,
    long SellerId,
    string Name,
    decimal PriceEtb,
    string Location,
    string Category,
    string Emoji,
    string? ImageUrl,
    bool IsActive,
    DateTime CreatedAt,
    int StockQuantity = 0,
    bool SellerVerified = false,
    string? Description = null,
    string LanguageCode = "en"
);


public record UpdateProductRequest(
    string Name,
    decimal PriceEtb,
    string Location,
    string Category,
    string Emoji,
    int StockQuantity = 0,
    string? Description = null,
    string? LanguageCode = null
);
