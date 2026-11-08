using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Merkato.Infrastructure.Services;

public class InspectionService : IInspectionService
{
    private readonly MerkatoDbContext _db;
    private readonly IEscrowService _escrow;
    private readonly IAuditLogService _audit;

    public InspectionService(MerkatoDbContext db, IEscrowService escrow, IAuditLogService audit)
    {
        _db = db;
        _escrow = escrow;
        _audit = audit;
    }

    public async Task<InspectionWindow> StartInspectionAsync(long orderId, CancellationToken ct = default)
    {
        var existing = await _db.InspectionWindows.FirstOrDefaultAsync(i => i.OrderId == orderId, ct);
        if (existing != null) return existing;

        var now = DateTime.UtcNow;
        var inspection = new InspectionWindow
        {
            OrderId = orderId,
            StartedAt = now,
            ExpiresAt = now.AddHours(48), // Server UTC 48-hour window
            Status = InspectionStatus.ACTIVE,
            CreatedAt = now
        };

        _db.InspectionWindows.Add(inspection);

        var order = await _db.Orders.FindAsync(new object[] { orderId }, ct);
        if (order != null)
        {
            order.EscrowState = EscrowState.INSPECTION_ACTIVE;
        }

        var escrow = await _db.EscrowAccounts.FirstOrDefaultAsync(e => e.OrderId == orderId, ct);
        if (escrow != null)
        {
            escrow.Status = EscrowState.INSPECTION_ACTIVE;
        }

        await _audit.LogAsync(null, "SYSTEM", "INSPECTION_STARTED", "INSPECTION", orderId.ToString(), "SUCCESS", "Started 48-hour window", ct);
        await _db.SaveChangesAsync(ct);

        return inspection;
    }

    public async Task<InspectionWindow> AcceptInspectionAsync(long orderId, long buyerId, CancellationToken ct = default)
    {
        var inspection = await _db.InspectionWindows.FirstOrDefaultAsync(i => i.OrderId == orderId, ct)
            ?? throw new KeyNotFoundException($"Inspection for order {orderId} not found");

        if (inspection.Status != InspectionStatus.ACTIVE)
        {
            throw new InvalidOperationException($"Cannot accept inspection with status {inspection.Status}");
        }

        inspection.Status = InspectionStatus.ACCEPTED_BY_BUYER;
        inspection.CompletedAt = DateTime.UtcNow;

        await _escrow.ReleaseFundsToMerchantAsync(orderId, buyerId, ct);
        await _audit.LogAsync(buyerId, "BUYER", "INSPECTION_ACCEPTED", "INSPECTION", orderId.ToString(), "SUCCESS", "Buyer accepted goods", ct);
        await _db.SaveChangesAsync(ct);

        return inspection;
    }

    public async Task<int> AutoReleaseExpiredInspectionsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.InspectionWindows
            .Where(i => i.Status == InspectionStatus.ACTIVE && i.ExpiresAt <= now)
            .ToListAsync(ct);

        int count = 0;
        foreach (var item in expired)
        {
            try
            {
                // Verify no active dispute
                var hasDispute = await _db.Disputes.AnyAsync(d => d.OrderId == item.OrderId && d.Status == DisputeStatus.OPEN, ct);
                if (hasDispute) continue;

                item.Status = InspectionStatus.AUTO_RELEASED;
                item.CompletedAt = now;

                await _escrow.ReleaseFundsToMerchantAsync(item.OrderId, 0L, ct);
                await _audit.LogAsync(null, "SYSTEM", "INSPECTION_AUTO_RELEASED", "INSPECTION", item.OrderId.ToString(), "SUCCESS", "48h window expired", ct);
                count++;
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(null, "SYSTEM", "INSPECTION_AUTO_RELEASE_FAIL", "INSPECTION", item.OrderId.ToString(), "FAILED", ex.Message, ct);
            }
        }

        if (count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return count;
    }
}

public class InspectionAutoReleaseWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InspectionAutoReleaseWorker> _logger;

    public InspectionAutoReleaseWorker(IServiceProvider serviceProvider, ILogger<InspectionAutoReleaseWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InspectionAutoReleaseWorker started at {Time}", DateTime.UtcNow);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var inspectionService = scope.ServiceProvider.GetRequiredService<IInspectionService>();
                int released = await inspectionService.AutoReleaseExpiredInspectionsAsync(stoppingToken);

                if (released > 0)
                {
                    _logger.LogInformation("Auto-released {Count} expired inspections at {Time}", released, DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in InspectionAutoReleaseWorker execution");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
