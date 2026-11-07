using System.ComponentModel.DataAnnotations;
using Merkato.Domain.Enums;

namespace Merkato.Domain.Entities;

public sealed class SellerPaymentAccount
{
    public long Id { get; set; }
    public long SellerId { get; set; }
    public PaymentRail PaymentRail { get; set; }
    [MaxLength(120)] public string AccountHolderName { get; set; } = string.Empty;
    [MaxLength(120)] public string AccountIdentifier { get; set; } = string.Empty;
    [MaxLength(120)] public string? BankName { get; set; }
    [MaxLength(500)] public string? Instructions { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
