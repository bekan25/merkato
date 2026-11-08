namespace Merkato.Application.DTOs;

public record CreateDeliveryDto(
    long OrderId,
    string CourierType,
    decimal FeeEtb,
    string RawOtp
);
