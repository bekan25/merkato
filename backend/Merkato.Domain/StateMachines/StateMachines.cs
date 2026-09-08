using Merkato.Domain.Enums;

namespace Merkato.Domain.StateMachines;

public static class EscrowStateMachine
{
    private static readonly Dictionary<EscrowState, HashSet<EscrowState>> AllowedTransitions = new()
    {
        [EscrowState.AWAITING_PAYMENT] = new() { EscrowState.HELD },
        [EscrowState.HELD] = new() { EscrowState.INSPECTION_ACTIVE, EscrowState.DISPUTED, EscrowState.REFUNDED },
        [EscrowState.INSPECTION_ACTIVE] = new() { EscrowState.RELEASED, EscrowState.DISPUTED, EscrowState.REFUNDED },
        [EscrowState.DISPUTED] = new() { EscrowState.RELEASED, EscrowState.REFUNDED },
        [EscrowState.RELEASED] = new(), // Terminal
        [EscrowState.REFUNDED] = new()  // Terminal
    };

    public static bool CanTransition(EscrowState current, EscrowState next)
    {
        return AllowedTransitions.TryGetValue(current, out var targets) && targets.Contains(next);
    }

    public static void ValidateTransition(EscrowState current, EscrowState next)
    {
        if (!CanTransition(current, next))
        {
            throw new InvalidOperationException($"Illegal escrow state transition from {current} to {next}");
        }
    }
}

public static class DeliveryStateMachine
{
    private static readonly Dictionary<DeliveryStatus, HashSet<DeliveryStatus>> AllowedTransitions = new()
    {
        [DeliveryStatus.CREATED] = new() { DeliveryStatus.ASSIGNED_COURIER, DeliveryStatus.FAILED_DELIVERY },
        [DeliveryStatus.ASSIGNED_COURIER] = new() { DeliveryStatus.PICKED_UP, DeliveryStatus.FAILED_DELIVERY },
        [DeliveryStatus.PICKED_UP] = new() { DeliveryStatus.IN_TRANSIT, DeliveryStatus.OUT_FOR_DELIVERY, DeliveryStatus.DELIVERED, DeliveryStatus.RETURNED_TO_MERCHANT },
        [DeliveryStatus.IN_TRANSIT] = new() { DeliveryStatus.OUT_FOR_DELIVERY, DeliveryStatus.DELIVERED, DeliveryStatus.FAILED_DELIVERY, DeliveryStatus.RETURNED_TO_MERCHANT },
        [DeliveryStatus.OUT_FOR_DELIVERY] = new() { DeliveryStatus.DELIVERED, DeliveryStatus.FAILED_DELIVERY },
        [DeliveryStatus.DELIVERED] = new(), // Terminal
        [DeliveryStatus.FAILED_DELIVERY] = new() { DeliveryStatus.RETURNED_TO_MERCHANT },
        [DeliveryStatus.RETURNED_TO_MERCHANT] = new() // Terminal
    };

    public static bool CanTransition(DeliveryStatus current, DeliveryStatus next)
    {
        return AllowedTransitions.TryGetValue(current, out var targets) && targets.Contains(next);
    }

    public static void ValidateTransition(DeliveryStatus current, DeliveryStatus next)
    {
        if (!CanTransition(current, next))
        {
            throw new InvalidOperationException($"Illegal delivery state transition from {current} to {next}");
        }
    }
}
