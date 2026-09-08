using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Merkato.Domain.Enums;

namespace Merkato.Domain.Entities;

public class User
{
    public long Id { get; set; }
    public string? FaydaIdNumber { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PinHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.BUYER;
    public bool IsFaydaVerified { get; set; } = false;
    public int TrustScore { get; set; } = 50;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Order
{
    public long Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public long BuyerId { get; set; }
    public long SellerId { get; set; }
    
    [Column(TypeName = "numeric(18,4)")]
    public decimal TotalAmountEtb { get; set; }
    
    [Column(TypeName = "numeric(18,4)")]
    public decimal DeliveryFeeEtb { get; set; }
    
    public PaymentRail PaymentRail { get; set; }
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






