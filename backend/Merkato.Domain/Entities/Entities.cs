using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Merkato.Domain.Enums;

namespace Merkato.Domain.Entities;

public class User
{
    public long Id { get; set; }
    public string? FaydaIdNumber { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = string.Empty;
    public byte[]? ProfilePhotoContent { get; set; }
    public string? ProfilePhotoContentType { get; set; }
    public string PinHash { get; set; } = string.Empty;
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LoginLockedUntil { get; set; }
    public bool IsSuspended { get; set; } = false;
    public int TokenVersion { get; set; } = 0;
    public UserRole Role { get; set; } = UserRole.BUYER;
    public bool IsFaydaVerified { get; set; } = false;
    public bool IsSellerVerified { get; set; } = false;
    public int TrustScore { get; set; } = 50;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Order
{
    public long? SourcingRequestId { get; set; }
    public long? SourcingOfferId { get; set; }
    public long Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public long BuyerId { get; set; }
    public long SellerId { get; set; }
    // Nullable for historical orders created before product references were persisted.
    public long? ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    [Column(TypeName = "numeric(18,4)")]
    public decimal UnitPriceEtb { get; set; }
    public InventoryReservationState InventoryReservationState { get; set; } = Merkato.Domain.Enums.InventoryReservationState.NONE;
    public DateTime? InventoryReservationExpiresAt { get; set; }
    
    [Column(TypeName = "numeric(18,4)")]
    public decimal TotalAmountEtb { get; set; }
    
    [Column(TypeName = "numeric(18,4)")]
    public decimal DeliveryFeeEtb { get; set; }

    // Immutable fee snapshot captured at checkout; later admin rate changes never rewrite existing orders.
    [Column(TypeName = "numeric(18,4)")]
    public decimal ProductSubtotalEtb { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal DiscountEtb { get; set; }
    [Column(TypeName = "numeric(7,4)")]
    public decimal MerchantCommissionRatePercent { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal MerchantCommissionEtb { get; set; }
    [Column(TypeName = "numeric(7,4)")]
    public decimal BuyerServiceFeeRatePercent { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal BuyerServiceFeeEtb { get; set; }
    
    public PaymentRail PaymentRail { get; set; }
    // ESCROW preserves the legacy held-funds workflow; SELLER_DIRECT is confirmed by the merchant.
    [MaxLength(24)]
    public string PaymentMode { get; set; } = "ESCROW";

    // Immutable seller destination captured at checkout. Never resolve old orders
    // against the seller's current account, which may have changed since purchase.
    [MaxLength(120)] public string? SellerPaymentAccountHolderName { get; set; }
    [MaxLength(120)] public string? SellerPaymentAccountIdentifier { get; set; }
    [MaxLength(120)] public string? SellerPaymentBankName { get; set; }
    [MaxLength(500)] public string? SellerPaymentInstructions { get; set; }
    public DateTime? SellerPaymentDestinationCapturedAt { get; set; }
    public string DeliveryCourier { get; set; } = "BEU_DELIVERY";
    public string ProductTitle { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string CityArea { get; set; } = "Addis Ababa";
    
    public EscrowState EscrowState { get; set; } = EscrowState.AWAITING_PAYMENT;
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.CREATED;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int RowVersion { get; set; } = 1;
}

public class PaymentTransaction
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? RailTransactionId { get; set; }
    public PaymentRail PaymentRail { get; set; }
    
    [Column(TypeName = "numeric(18,4)")]
    public decimal AmountEtb { get; set; }
    public string Currency { get; set; } = "ETB";
    public PaymentStatus Status { get; set; } = PaymentStatus.INITIATED;
    public bool WebhookVerified { get; set; } = false;
    public string? RawPayloadHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
}

public class EscrowAccount
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long BuyerId { get; set; }
    public long SellerId { get; set; }
    
    [Column(TypeName = "numeric(18,4)")]
    public decimal HeldAmountEtb { get; set; } = 0m;
    
    [Column(TypeName = "numeric(18,4)")]
    public decimal DisputedAmountEtb { get; set; } = 0m;
    
    [Column(TypeName = "numeric(18,4)")]
    public decimal ReleasedAmountEtb { get; set; } = 0m;
    
    [Column(TypeName = "numeric(18,4)")]
    public decimal RefundedAmountEtb { get; set; } = 0m;
    
    public EscrowState Status { get; set; } = EscrowState.HELD;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int RowVersion { get; set; } = 1;
}

public class LedgerTransaction
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // ESCROW_LOCK, ESCROW_RELEASE, ESCROW_REFUND
    public string ReferenceId { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<LedgerEntry> Entries { get; set; } = new();
}

public class LedgerEntry
{
    public long Id { get; set; }
    public long LedgerTransactionId { get; set; }
    public LedgerAccountType AccountType { get; set; }
    public long? UserId { get; set; }
    public LedgerEntryType EntryType { get; set; } // DEBIT, CREDIT
    
    [Column(TypeName = "numeric(18,4)")]
    public decimal AmountEtb { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class InspectionWindow
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } // StartedAt + 48 hours
    public InspectionStatus Status { get; set; } = InspectionStatus.ACTIVE;
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Dispute
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long BuyerId { get; set; }
    public long SellerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; } = DisputeStatus.OPEN;
    public long? ArbiterId { get; set; }
    public string? ArbiterDecisionNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

public class Delivery
{
    public long Id { get; set; }
    public string DeliveryId { get; set; } = string.Empty;
    public long OrderId { get; set; }
    public string CourierType { get; set; } = "BEU_DELIVERY";
    public DeliveryStatus Status { get; set; } = DeliveryStatus.CREATED;
    public string? RiderName { get; set; }
    public string? RiderPhone { get; set; }
    public string OtpHash { get; set; } = string.Empty;
    public string OtpSalt { get; set; } = string.Empty;
    
    [Column(TypeName = "numeric(18,4)")]
    public decimal FeeEtb { get; set; } = 0m;
    
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int RowVersion { get; set; } = 1;
}

public class OtpAttemptTracker
{
    public long Id { get; set; }
    public string DeliveryId { get; set; } = string.Empty;
    public int FailedAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }
    public bool IsConsumed { get; set; } = false;
    public DateTime? ConsumedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public long Id { get; set; }
    public Guid EventId { get; set; } = Guid.NewGuid();
    public long? ActorId { get; set; }
    public string ActorRole { get; set; } = "SYSTEM";
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? TraceId { get; set; }
    public string Result { get; set; } = "SUCCESS";
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class IdempotencyRecord
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Product
{
    public long Id { get; set; }
    public long SellerId { get; set; }

    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "numeric(18,4)")]
    public decimal PriceEtb { get; set; }

    public string Location { get; set; } = "Addis Ababa";
    public string Category { get; set; } = "Other";
    public string Emoji { get; set; } = "???";
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}








// Wholesale requests are quotes, not paid orders. Payment uses verified checkout separately.
public class WholesalePriceTier
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long SellerId { get; set; }
    public int MinimumQuantity { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal UnitPriceEtb { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class WholesaleRequest
{
    public long Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public long ProductId { get; set; }
    public long BuyerId { get; set; }
    public long SellerId { get; set; }
    public int Quantity { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal RequestedUnitPriceEtb { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal? QuotedUnitPriceEtb { get; set; }
    public string Status { get; set; } = "REQUESTED";
    public string? BuyerNote { get; set; }
    public string? SellerNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AffiliateProfile
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string ReferralCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AffiliateReferral
{
    public long Id { get; set; }
    public long AffiliateUserId { get; set; }
    public string ReferralCode { get; set; } = string.Empty;
    public string VisitorKeyHash { get; set; } = string.Empty;
    public long? ReferredUserId { get; set; }
    public DateTime ClickedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RegisteredAt { get; set; }
}

public class AffiliateCommission
{
    public long Id { get; set; }
    public long AffiliateUserId { get; set; }
    public long? AffiliateReferralId { get; set; }
    public long OrderId { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal EligibleAmountEtb { get; set; }
    [Column(TypeName = "numeric(7,4)")]
    public decimal RatePercent { get; set; }
    [Column(TypeName = "numeric(18,4)")]
    public decimal CommissionEtb { get; set; }
    public string Status { get; set; } = "AVAILABLE";
    public string? PayoutReference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
}


public sealed class UserAddress
{
 public long Id { get; set; }
 public long UserId { get; set; }
 public string Label { get; set; } = "Home";
 public string RecipientName { get; set; } = string.Empty;
 public string PhoneNumber { get; set; } = string.Empty;
 public string City { get; set; } = string.Empty;
 public string SubCity { get; set; } = string.Empty;
 public string? Woreda { get; set; }
 public string? StreetAddress { get; set; }
 public string? DeliveryInstructions { get; set; }
 public bool IsDefault { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
