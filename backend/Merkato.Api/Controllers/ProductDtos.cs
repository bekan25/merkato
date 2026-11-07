using Merkato.Domain.Entities;

namespace Merkato.Api.Controllers;

public record CreateProductRequest(
    long SellerId,
    string Name,
    decimal PriceEtb,
    string Location,
    string Category,
    string Emoji,
    int StockQuantity = 0
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
    bool SellerVerified = false
);


public record UpdateProductRequest(
    string Name,
    decimal PriceEtb,
    string Location,
    string Category,
    string Emoji,
    int StockQuantity = 0
);
