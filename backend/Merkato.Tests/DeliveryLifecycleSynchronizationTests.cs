using Microsoft.EntityFrameworkCore;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Merkato.Infrastructure.Services;
using Xunit;

namespace Merkato.Tests;

public sealed class DeliveryLifecycleSynchronizationTests
{
    private static MerkatoDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MerkatoDbContext(options);
    }

    [Fact]
    public async Task CourierStatusChangesStaySynchronizedWithOrderAndKeepPickupTime()
    {
        await using var db = CreateDb(nameof(CourierStatusChangesStaySynchronizedWithOrderAndKeepPickupTime));
        var previousUpdate = DateTime.UtcNow.AddHours(-1);
        db.Orders.Add(new Order { Id = 1, BuyerId = 10, SellerId = 20, OrderNumber = "MK-1", DeliveryStatus = DeliveryStatus.ASSIGNED_COURIER, UpdatedAt = previousUpdate });
        db.Deliveries.Add(new Delivery { Id = 1, OrderId = 1, DeliveryId = "DEL-1", Status = DeliveryStatus.ASSIGNED_COURIER });
        await db.SaveChangesAsync();
        var service = new DeliveryService(db, null!, null!);

        var pickedUp = await service.UpdateDeliveryStatusAsync("DEL-1", DeliveryStatus.PICKED_UP);
        var dispatchedAt = pickedUp.DispatchedAt;
        Assert.NotNull(dispatchedAt);
        Assert.Equal(DeliveryStatus.PICKED_UP, (await db.Orders.SingleAsync(x => x.Id == 1)).DeliveryStatus);

        await service.UpdateDeliveryStatusAsync("DEL-1", DeliveryStatus.IN_TRANSIT);
        await service.UpdateDeliveryStatusAsync("DEL-1", DeliveryStatus.OUT_FOR_DELIVERY);
        await service.UpdateDeliveryStatusAsync("DEL-1", DeliveryStatus.FAILED_DELIVERY);
        await service.UpdateDeliveryStatusAsync("DEL-1", DeliveryStatus.RETURNED_TO_MERCHANT);

        var finalOrder = await db.Orders.SingleAsync(x => x.Id == 1);
        var finalDelivery = await db.Deliveries.SingleAsync(x => x.DeliveryId == "DEL-1");
        Assert.Equal(DeliveryStatus.RETURNED_TO_MERCHANT, finalOrder.DeliveryStatus);
        Assert.Equal(finalDelivery.Status, finalOrder.DeliveryStatus);
        Assert.Equal(dispatchedAt, finalDelivery.DispatchedAt);
        Assert.True(finalOrder.UpdatedAt > previousUpdate);
    }

    [Fact]
    public async Task IllegalCourierTransitionDoesNotChangeEitherStatus()
    {
        await using var db = CreateDb(nameof(IllegalCourierTransitionDoesNotChangeEitherStatus));
        db.Orders.Add(new Order { Id = 2, BuyerId = 10, SellerId = 20, OrderNumber = "MK-2", DeliveryStatus = DeliveryStatus.FAILED_DELIVERY });
        db.Deliveries.Add(new Delivery { Id = 2, OrderId = 2, DeliveryId = "DEL-2", Status = DeliveryStatus.FAILED_DELIVERY });
        await db.SaveChangesAsync();
        var service = new DeliveryService(db, null!, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateDeliveryStatusAsync("DEL-2", DeliveryStatus.PICKED_UP));

        Assert.Equal(DeliveryStatus.FAILED_DELIVERY, (await db.Orders.SingleAsync(x => x.Id == 2)).DeliveryStatus);
        Assert.Equal(DeliveryStatus.FAILED_DELIVERY, (await db.Deliveries.SingleAsync(x => x.Id == 2)).Status);
    }
}
