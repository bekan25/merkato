using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Services;

public sealed class InventoryReservationExpiryService(
    IServiceScopeFactory scopeFactory,
    ILogger<InventoryReservationExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MerkatoDbContext>();
                var now = DateTime.UtcNow;
                var expiredOrderIds = await db.Orders.AsNoTracking()
                    .Where(x => x.InventoryReservationState == InventoryReservationState.RESERVED &&
                                x.InventoryReservationExpiresAt <= now)
                    .OrderBy(x => x.InventoryReservationExpiresAt)
                    .Select(x => x.Id)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                foreach (var orderId in expiredOrderIds)
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
                    var productId = await db.Orders.AsNoTracking()
                        .Where(x => x.Id == orderId)
                        .Select(x => x.ProductId)
                        .FirstOrDefaultAsync(stoppingToken);

                    var claimed = await db.Orders
                        .Where(x => x.Id == orderId &&
                                    x.InventoryReservationState == InventoryReservationState.RESERVED &&
                                    x.InventoryReservationExpiresAt <= now)
                        .ExecuteUpdateAsync(update => update
                            .SetProperty(x => x.InventoryReservationState, InventoryReservationState.EXPIRED)
                            .SetProperty(x => x.UpdatedAt, now), stoppingToken);

                    if (claimed == 1)
                    {
                        if (!productId.HasValue)
                            throw new InvalidOperationException($"Reserved order {orderId} has no product reference.");

                        var released = await db.ProductListingDetails
                            .Where(x => x.ProductId == productId.Value && x.ReservedQuantity >= 1)
                            .ExecuteUpdateAsync(update => update
                                .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity - 1)
                                .SetProperty(x => x.UpdatedAt, now), stoppingToken);
                        if (released != 1)
                            throw new InvalidOperationException($"Could not release inventory for expired order {orderId}.");
                    }

                    await transaction.CommitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Inventory reservation expiry cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
