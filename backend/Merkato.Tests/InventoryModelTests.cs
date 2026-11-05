using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public sealed class InventoryModelTests
{
    [Fact]
    public void NewOrders_StartWithoutInventoryReservation()
    {
        var order = new Order();
        Assert.Equal(Merkato.Domain.Enums.InventoryReservationState.NONE, order.InventoryReservationState);
        Assert.Null(order.InventoryReservationExpiresAt);
    }

    [Fact]
    public void ProductListingDetail_MapsReservedStockWithSafeDefault()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseInMemoryDatabase(nameof(ProductListingDetail_MapsReservedStockWithSafeDefault))
            .Options;
        using var db = new MerkatoDbContext(options);

        var detail = new ProductListingDetail { ProductId = 10, StockQuantity = 8 };
        Assert.Equal(0, detail.ReservedQuantity);

        var entity = db.Model.FindEntityType(typeof(ProductListingDetail));
        Assert.NotNull(entity);
        Assert.NotNull(entity!.FindProperty(nameof(ProductListingDetail.ReservedQuantity)));

        var orderEntity = db.Model.FindEntityType(typeof(Order));
        Assert.NotNull(orderEntity);
        Assert.NotNull(orderEntity!.FindProperty(nameof(Order.InventoryReservationState)));
        Assert.NotNull(orderEntity.FindProperty(nameof(Order.InventoryReservationExpiresAt)));
    }
}
