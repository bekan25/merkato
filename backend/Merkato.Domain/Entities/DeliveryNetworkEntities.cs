using System.ComponentModel.DataAnnotations.Schema;

namespace Merkato.Domain.Entities;

public sealed class DeliveryProvider
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ProviderKind { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool SupportsQuotes { get; set; }
    public bool SupportsBooking { get; set; }
    public bool SupportsTracking { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DeliveryPartnerProfile
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string ApprovalStatus { get; set; } = "PENDING";
    public string ServiceAreasJson { get; set; } = "[]";
    public string TransportMode { get; set; } = string.Empty;
    public string? VehicleDescription { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal? CapacityKg { get; set; }
    public long? ReviewerUserId { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}

public sealed class DeliveryAssignment
{
    public long Id { get; set; }
    public long DeliveryId { get; set; }
    public long ProviderId { get; set; }
    public long? PartnerUserId { get; set; }
    public long AssignedByUserId { get; set; }
    public string Status { get; set; } = "OFFERED";
    [Column(TypeName = "numeric(18,4)")]
    public decimal AgreedFeeEtb { get; set; }
    public string Currency { get; set; } = "ETB";
    public string? ExternalBookingReference { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class DeliveryTrackingEvent
{
    public long Id { get; set; }
    public long DeliveryId { get; set; }
    public long? AssignmentId { get; set; }
    public string Source { get; set; } = "MERKATO";
    public string EventKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
