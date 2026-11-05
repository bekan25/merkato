namespace Merkato.Domain.Entities;

public sealed class WishlistItem
{
    public long Id { get; set; }
    public long BuyerId { get; set; }
    public long ProductId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PriceAlert
{
    public long Id { get; set; }
    public long BuyerId { get; set; }
    public long ProductId { get; set; }
    public decimal TargetPriceEtb { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TriggeredAt { get; set; }
}

public sealed class UserNotification
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Type { get; set; } = "SYSTEM";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
