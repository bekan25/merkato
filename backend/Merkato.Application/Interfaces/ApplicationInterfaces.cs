using Merkato.Application.DTOs;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;

namespace Merkato.Application.Interfaces;

public interface ICurrentUserService
{
    long UserId { get; }
    UserRole Role { get; }
    bool IsAuthenticated { get; }
}

public interface IEscrowService
{
    Task<EscrowAccount> LockFundsAsync(long orderId, decimal amountEtb, CancellationToken ct = default);
    Task<EscrowAccount> ReleaseFundsToMerchantAsync(long orderId, long requesterId, CancellationToken ct = default);
    Task<EscrowAccount> RefundFundsToBuyerAsync(long orderId, long requesterId, string reason, CancellationToken ct = default);
    Task<EscrowAccount> FileDisputeAsync(long orderId, long requesterId, string reason, CancellationToken ct = default);
}

public interface ILedgerEngine
{
    Task RecordTransactionAsync(
        long orderId,
        string transactionType,
        string referenceId,
        List<(LedgerAccountType account, LedgerEntryType type, decimal amount, long? userId)> entries,
        CancellationToken ct = default
    );
}

public interface IPaymentService
{
    Task<PaymentTransaction> InitiatePaymentAsync(long orderId, string idempotencyKey, decimal amountEtb, PaymentRail rail, CancellationToken ct = default);
    Task<PaymentTransaction> ProcessWebhookAsync(string provider, PaymentWebhookDto dto, string rawPayload, string signature, CancellationToken ct = default);
}

public interface IDeliveryService
{
    Task<Delivery> CreateDeliveryAsync(long orderId, string courierType, decimal feeEtb, string rawOtp, CancellationToken ct = default);
    Task<Delivery> VerifyOtpAndCompleteHandoverAsync(string deliveryId, long requesterId, string rawOtp, CancellationToken ct = default);
    Task<Delivery> UpdateDeliveryStatusAsync(string deliveryId, DeliveryStatus nextStatus, CancellationToken ct = default);
}

public interface IInspectionService
{
    Task<InspectionWindow> StartInspectionAsync(long orderId, CancellationToken ct = default);
    Task<InspectionWindow> AcceptInspectionAsync(long orderId, long buyerId, CancellationToken ct = default);
    Task<int> AutoReleaseExpiredInspectionsAsync(CancellationToken ct = default);
}

public interface IKycService
{
    Task<User> VerifyFaydaAsync(long userId, string faydaIdNumber, CancellationToken ct = default);
}

public interface ITrustScoreService
{
    Task<int> CalculateUserTrustScoreAsync(long userId, CancellationToken ct = default);
}

public interface IRiskAssessmentService
{
    Task<RiskDecision> EvaluateOrderRiskAsync(long userId, decimal orderAmountEtb, CancellationToken ct = default);
}

public interface IAuditLogService
{
    Task LogAsync(long? actorId, string actorRole, string action, string entityType, string entityId, string result, string? metadata = null, CancellationToken ct = default);
}
