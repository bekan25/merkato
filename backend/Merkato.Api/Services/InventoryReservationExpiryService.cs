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
                                x.InventoryReservationExpiresAt <= now &&
                                !db.PaymentTransactions.Any(p => p.OrderId == x.Id && p.Status == PaymentStatus.VERIFIED) &&
                                !db.PaymentReceipts.Any(r => r.OrderId == x.Id && r.Status == "PENDING_REVIEW"))
                    .OrderBy(x => x.InventoryReservationExpiresAt)
                    .Select(x => x.Id)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                foreach (var orderId in expiredOrderIds)
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
                    var productId = await db.Orders.AsNoTracking()
                        .Where(x => x.Id == orderId)
                        .Select(x => new { x.ProductId, x.Quantity })
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
                        if (productId?.ProductId is not long reservedProductId || productId.Quantity < 1)
                            throw new InvalidOperationException($"Reserved order {orderId} has invalid product or quantity data.");

                        var released = await db.ProductListingDetails
                            .Where(x => x.ProductId == reservedProductId && x.ReservedQuantity >= productId.Quantity)
                            .ExecuteUpdateAsync(update => update
                                .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity - productId.Quantity)
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
