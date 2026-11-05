using System.ComponentModel.DataAnnotations.Schema;

namespace Merkato.Domain.Entities;

public enum ProductCondition { NEW = 0, USED = 1, REFURBISHED = 2 }
public enum GroupBuyStatus { OPEN = 0, TARGET_REACHED = 1, EXPIRED = 2, CANCELLED = 3 }
public enum ReturnStatus { REQUESTED = 0, APPROVED = 1, REJECTED = 2, RECEIVED = 3, REFUNDED = 4 }

public sealed class ProductListingDetail
{
    public long ProductId { get; set; }
    public ProductCondition Condition { get; set; } = ProductCondition.NEW;
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ProductReview
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long BuyerId { get; set; }
    public long OrderId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ProductPriceSnapshot
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long SellerId { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal PriceEtb { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

public sealed class GroupBuy
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long OrganizerId { get; set; }
    public int TargetQuantity { get; set; }
    public int JoinedQuantity { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal RegularUnitPriceEtb { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal DiscountUnitPriceEtb { get; set; }
    public DateTime ExpiresAt { get; set; }
    public GroupBuyStatus Status { get; set; } = GroupBuyStatus.OPEN;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class GroupBuyMember
{
    public long Id { get; set; }
    public long GroupBuyId { get; set; }
    public long BuyerId { get; set; }
    public int Quantity { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ReturnRequest
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long BuyerId { get; set; }
    public long SellerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrl { get; set; }
    public string? ResolutionNote { get; set; }
    public ReturnStatus Status { get; set; } = ReturnStatus.REQUESTED;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class LoyaltyAccount
{
    public long UserId { get; set; }
    public int PointsBalance { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class LoyaltyTransaction
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long? OrderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
