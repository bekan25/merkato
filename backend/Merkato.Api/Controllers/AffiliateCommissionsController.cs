using System.Security.Claims;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/v1/affiliates/commissions")]
[Authorize]
public sealed class AffiliateCommissionsController(MerkatoDbContext db) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var query = db.AffiliateCommissions.AsNoTracking().Where(x => x.AffiliateUserId == userId);
        var totals = await query.GroupBy(_ => 1).Select(g => new
        {
            heldEtb = g.Where(x => x.Status == "HELD").Sum(x => (decimal?)x.CommissionEtb) ?? 0m,
            availableEtb = g.Where(x => x.Status == "AVAILABLE").Sum(x => (decimal?)x.CommissionEtb) ?? 0m,
            paidEtb = g.Where(x => x.Status == "PAID").Sum(x => (decimal?)x.CommissionEtb) ?? 0m,
            reversedEtb = g.Where(x => x.Status == "REVERSED").Sum(x => (decimal?)x.CommissionEtb) ?? 0m
        }).FirstOrDefaultAsync(ct);

        var items = await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id, x.OrderId, x.EligibleAmountEtb, x.RatePercent,
                x.CommissionEtb, x.Status, x.PayoutReference, x.CreatedAt, x.PaidAt
            }).ToListAsync(ct);

        return Ok(new
        {
            totals = totals ?? new { heldEtb = 0m, availableEtb = 0m, paidEtb = 0m, reversedEtb = 0m },
            items
        });
    }

    [HttpGet("admin")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Admin([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 10000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.AffiliateCommissions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim().ToUpperInvariant());

        var totals = await db.AffiliateCommissions.GroupBy(_ => 1).Select(g => new
        {
            commissionCount = g.Count(),
            heldEtb = g.Where(x => x.Status == "HELD").Sum(x => (decimal?)x.CommissionEtb) ?? 0m,
            availableEtb = g.Where(x => x.Status == "AVAILABLE").Sum(x => (decimal?)x.CommissionEtb) ?? 0m,
            paidEtb = g.Where(x => x.Status == "PAID").Sum(x => (decimal?)x.CommissionEtb) ?? 0m,
            reversedEtb = g.Where(x => x.Status == "REVERSED").Sum(x => (decimal?)x.CommissionEtb) ?? 0m
        }).FirstOrDefaultAsync(ct);

        var items = await (from commission in query
            join affiliate in db.Users.AsNoTracking() on commission.AffiliateUserId equals affiliate.Id
            join order in db.Orders.AsNoTracking() on commission.OrderId equals order.Id
            orderby commission.CreatedAt descending
            select new
            {
                commission.Id, commission.AffiliateUserId, affiliateName = affiliate.FullName,
                commission.OrderId, order.OrderNumber, commission.EligibleAmountEtb,
                commission.RatePercent, commission.CommissionEtb, commission.Status,
                commission.PayoutReference, commission.CreatedAt, commission.PaidAt
            }).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(new
        {
            page, pageSize,
            totals = totals ?? new { commissionCount = 0, heldEtb = 0m, availableEtb = 0m, paidEtb = 0m, reversedEtb = 0m },
            items
        });
    }

    [HttpPost("{commissionId:long}/payout")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> MarkPaid(long commissionId, [FromBody] AffiliatePayoutRequest input, CancellationToken ct)
    {
        var reference = input.PayoutReference?.Trim();
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 120)
            return BadRequest(new { error = "A payout reference (1-120 characters) is required." });

        var paidAt = DateTime.UtcNow;
        // Compare-and-set prevents two admins from paying the same commission concurrently.
        var updated = await db.AffiliateCommissions
            .Where(x => x.Id == commissionId && x.Status == "AVAILABLE")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "PAID")
                .SetProperty(x => x.PayoutReference, reference)
                .SetProperty(x => x.PaidAt, paidAt), ct);
        if (updated == 0)
        {
            var current = await db.AffiliateCommissions.AsNoTracking()
                .Where(x => x.Id == commissionId)
                .Select(x => new { x.Id, x.Status })
                .FirstOrDefaultAsync(ct);
            if (current is null) return NotFound();
            return Conflict(new { error = "Only AVAILABLE commissions can be marked as paid.", currentStatus = current.Status });
        }

        var commission = await db.AffiliateCommissions.AsNoTracking()
            .Where(x => x.Id == commissionId)
            .Select(x => new { x.Id, x.Status, x.PayoutReference, x.PaidAt })
            .SingleAsync(ct);
        return Ok(commission);
    }
}

public sealed record AffiliatePayoutRequest(string PayoutReference);
