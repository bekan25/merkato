using System.Security.Claims;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Authorize(Roles = "BUYER")]
[Route("api/returns")]
public sealed class ReturnsController(MerkatoDbContext db) : ControllerBase
{
    private long BuyerId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var rows = await db.ReturnRequests.AsNoTracking().Where(x => x.BuyerId == BuyerId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.OrderId, x.Reason, x.Status, x.ResolutionNote, x.CreatedAt, x.UpdatedAt })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> RequestReturn(ReturnRequestInput input, CancellationToken ct)
    {
        var reason = input.Reason?.Trim();
        if (input.OrderId <= 0 || string.IsNullOrWhiteSpace(reason) || reason.Length > 1200)
            return BadRequest(new { message = "Provide a valid order and a return reason (up to 1200 characters)." });
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == input.OrderId && x.BuyerId == BuyerId, ct);
        if (order is null) return NotFound();
        if (order.DeliveryStatus != DeliveryStatus.DELIVERED || order.EscrowState is EscrowState.CANCELLED or EscrowState.REFUNDED)
            return Conflict(new { message = "A return can only be requested for a delivered, non-cancelled order." });
        if (await db.ReturnRequests.AnyAsync(x => x.OrderId == order.Id, ct))
            return Conflict(new { message = "A return request already exists for this order." });
        var row = new ReturnRequest { OrderId = order.Id, BuyerId = BuyerId, SellerId = order.SellerId, Reason = reason, Status = ReturnStatus.REQUESTED, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.ReturnRequests.Add(row);
        await db.SaveChangesAsync(ct);
        return Accepted(new { row.Id, row.OrderId, status = row.Status.ToString(), row.CreatedAt, message = "Return request submitted for merchant review. No refund has been issued." });
    }
}

public sealed record ReturnRequestInput(long OrderId, string Reason);