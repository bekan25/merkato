using Merkato.Domain.Enums;
using Merkato.Domain.StateMachines;
using Xunit;

namespace Merkato.Tests;

public sealed class OrderDeliveryLifecycleTests
{
    [Theory]
    [InlineData(DeliveryStatus.CREATED, DeliveryStatus.ASSIGNED_COURIER)]
    [InlineData(DeliveryStatus.ASSIGNED_COURIER, DeliveryStatus.PICKED_UP)]
    [InlineData(DeliveryStatus.PICKED_UP, DeliveryStatus.IN_TRANSIT)]
    [InlineData(DeliveryStatus.IN_TRANSIT, DeliveryStatus.OUT_FOR_DELIVERY)]
    [InlineData(DeliveryStatus.OUT_FOR_DELIVERY, DeliveryStatus.DELIVERED)]
    [InlineData(DeliveryStatus.FAILED_DELIVERY, DeliveryStatus.RETURNED_TO_MERCHANT)]
    public void Valid_delivery_transitions_are_accepted(DeliveryStatus current, DeliveryStatus next)
    {
        Assert.True(DeliveryStateMachine.CanTransition(current, next));
        DeliveryStateMachine.ValidateTransition(current, next);
    }

    [Theory]
    [InlineData(DeliveryStatus.DELIVERED, DeliveryStatus.IN_TRANSIT)]
    [InlineData(DeliveryStatus.RETURNED_TO_MERCHANT, DeliveryStatus.DELIVERED)]
    [InlineData(DeliveryStatus.CREATED, DeliveryStatus.DELIVERED)]
    [InlineData(DeliveryStatus.ASSIGNED_COURIER, DeliveryStatus.OUT_FOR_DELIVERY)]
    public void Invalid_or_terminal_delivery_transitions_are_rejected(DeliveryStatus current, DeliveryStatus next)
    {
        Assert.False(DeliveryStateMachine.CanTransition(current, next));
        Assert.Throws<InvalidOperationException>(() => DeliveryStateMachine.ValidateTransition(current, next));
    }
}
