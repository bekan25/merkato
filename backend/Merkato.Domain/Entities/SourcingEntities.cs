namespace Merkato.Domain.Entities;
public sealed class SourcingRequest
{
 public long Id { get; set; }
 public string RequestNumber { get; set; } = string.Empty;
 public long BuyerId { get; set; }
 public string Title { get; set; } = string.Empty;
 public string Description { get; set; } = string.Empty;
 public string Category { get; set; } = string.Empty;
 public string City { get; set; } = "Addis Ababa";
 public int Quantity { get; set; }
 public decimal? TargetUnitPriceEtb { get; set; }
 public string Status { get; set; } = "OPEN";
 public DateTime ExpiresAt { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class SourcingOffer
{
 public long Id { get; set; }
 public long SourcingRequestId { get; set; }
 public long SellerId { get; set; }
 public long ProductId { get; set; }
 public decimal UnitPriceEtb { get; set; }
 public int AvailableQuantity { get; set; }
 public string? Note { get; set; }
 public int? LeadTimeDays { get; set; }
 public string Status { get; set; } = "SUBMITTED";
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}