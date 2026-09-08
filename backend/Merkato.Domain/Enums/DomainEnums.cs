namespace Merkato.Domain.Enums;

public enum UserRole
{
    BUYER,
    MERCHANT,
    COURIER,
    INSPECTOR,
    ARBITER,
    ADMIN
}

public enum PaymentRail
{
    TELEBIRR,
    CBE_BIRR,
    AWASH_BIRR,
    BANK_OF_ABYSSINIA,
    SANDBOX
}

public enum PaymentStatus
{
    INITIATED,
    PENDING,
    VERIFIED,
    FAILED,
    REFUNDED
}

public enum EscrowState
{
    AWAITING_PAYMENT,
    HELD,
    INSPECTION_ACTIVE,
    RELEASED,
    REFUNDED,
    DISPUTED
}

public enum DeliveryStatus
{
    CREATED,
    ASSIGNED_COURIER,
    PICKED_UP,
    IN_TRANSIT,
    OUT_FOR_DELIVERY,
    DELIVERED,
    FAILED_DELIVERY,
    RETURNED_TO_MERCHANT
}

public enum InspectionStatus
{
    ACTIVE,
    ACCEPTED_BY_BUYER,
    AUTO_RELEASED,
    DISPUTED
}

public enum DisputeStatus
{
    OPEN,
    UNDER_REVIEW,
    RESOLVED_REFUND,
    RESOLVED_RELEASE
}

public enum KycVerificationStatus
{
    UNVERIFIED,
    PENDING,
    VERIFIED,
    REJECTED
}

public enum RiskDecision
{
    ALLOW,
    STEP_UP_REQUIRED,
    MANUAL_REVIEW_HOLD,
    TRANSACTION_BLOCKED
}

public enum LedgerEntryType
{
    DEBIT,
    CREDIT
}

public enum LedgerAccountType
{
    BUYER_PAYABLE,
    ESCROW_LIABILITY,
    MERCHANT_RECEIVABLE,
    PLATFORM_FEE,
    DISPUTE_RESERVE
}
