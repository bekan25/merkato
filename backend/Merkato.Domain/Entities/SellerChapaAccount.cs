using System.ComponentModel.DataAnnotations;

namespace Merkato.Domain.Entities;

/// <summary>
/// Seller-owned Chapa subaccount identity and onboarding state.
/// Provider credentials and full bank details are never stored here.
/// </summary>
public sealed class SellerChapaAccount
{
    public long Id { get; set; }
    public long SellerId { get; set; }
    [MaxLength(120)] public string? ChapaSubaccountId { get; set; }
    [MaxLength(120)] public string? ProviderReference { get; set; }
    [MaxLength(120)] public string? BusinessName { get; set; }
    [MaxLength(120)] public string? AccountName { get; set; }
    [MaxLength(40)] public string? BankCode { get; set; }
    [MaxLength(4)] public string? AccountNumberLast4 { get; set; }
    [MaxLength(24)] public string OnboardingStatus { get; set; } = "NOT_STARTED";
    [MaxLength(500)] public string? ReviewNote { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
