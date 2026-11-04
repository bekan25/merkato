using Merkato.Domain.Enums;

namespace Merkato.Application.DTOs;

public record RegisterUserDto(
    string PhoneNumber,
    string FullName,
    string? FaydaIdNumber,
    UserRole Role,
    string Pin
);

public record LoginDto(
    string PhoneNumber,
    string Pin
);

public record AuthResponseDto(
    string Token,
    long UserId,
    string PhoneNumber,
    string FullName,
    UserRole Role,
    int TrustScore,
    bool IsFaydaVerified
);

public record CreateOrderDto(
    long SellerId,
    string ProductTitle,
    decimal AmountEtb,
    decimal DeliveryFeeEtb,
    PaymentRail PaymentRail,
    string DeliveryCourier,
    string DeliveryAddress,
    string CityArea
);

public record OrderResponseDto(
    long Id,
    string OrderNumber,
    long BuyerId,
    long SellerId,
    string ProductTitle,
    decimal TotalAmountEtb,
    decimal DeliveryFeeEtb,
    PaymentRail PaymentRail,
    string DeliveryCourier,
    string DeliveryAddress,
    EscrowState EscrowState,
    DeliveryStatus DeliveryStatus,
    DateTime CreatedAt
);

public record PaymentWebhookDto(
    string EventId,
    string RailTransactionId,
    string OrderNumber,
    decimal AmountEtb,
    string Currency,
    string Status
);

public record VerifyOtpDto(
    string DeliveryId,
    string OtpCode
);

public record InspectionResponseDto(
    long Id,
    long OrderId,
    DateTime StartedAt,
    DateTime ExpiresAt,
    InspectionStatus Status,
    long RemainingSeconds
);

public record CreateDisputeDto(
    long OrderId,
    string Reason,
    string Description,
    List<string>? EvidenceUrls
);

public record ArbitrateDisputeDto(
    long DisputeId,
    bool ApproveBuyerRefund,
    string Notes
);


