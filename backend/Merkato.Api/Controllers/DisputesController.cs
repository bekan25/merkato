using System.Security.Claims;
using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/disputes")]
public sealed class DisputesController(MerkatoDbContext db, IEscrowService escrow) : ControllerBase
{
    private long CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Only administrators can inspect the platform-wide dispute queue.
    [HttpGet]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var disputes = await db.Disputes.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.OrderId,
                x.BuyerId,
                x.SellerId,
                x.Reason,
                x.Description,
                status = x.Status.ToString(),
                x.ArbiterId,
                x.ArbiterDecisionNotes,
                x.CreatedAt,
                x.ResolvedAt
            })
            .ToListAsync(ct);

        return Ok(disputes);
    }

    // A dispute must be backed by an actual held escrow balance. Manual
    // buyer-to-seller transfers are not escrow and cannot be frozen/refunded here.
    [HttpPost]
    [Authorize(Roles = "BUYER")]
    public async Task<IActionResult> File(DisputeInput input, CancellationToken ct)
    {
        var reason = input.Reason?.Trim();
        var description = input.Description?.Trim();

        if (input.OrderId <= 0 ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 120 ||
            string.IsNullOrWhiteSpace(description) || description.Length < 10 || description.Length > 2000)
        {
            return BadRequest(new { message = "Provide an order, a reason (up to 120 characters), and a description (10–2000 characters)." });
        }

        var order = await db.Orders.FirstOrDefaultAsync(
            x => x.Id == input.OrderId && x.BuyerId == CurrentUserId, ct);
        if (order is null) return NotFound(new { message = "Order not found for this buyer account." });

        if (await db.Disputes.AnyAsync(x => x.OrderId == order.Id, ct))
            return Conflict(new { message = "A dispute has already been filed for this order." });

        var escrowAccount = await db.EscrowAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == order.Id, ct);
        if (escrowAccount is null || escrowAccount.Status != EscrowState.HELD || escrowAccount.HeldAmountEtb <= 0m)
            return Conflict(new
            {
                message = "This order has no funds held in MERKATO escrow. Direct seller transfers must be resolved between buyer and seller; MERKATO cannot freeze or refund those transfers."
            });

        await escrow.FileDisputeAsync(order.Id, CurrentUserId, reason, ct);

        var dispute = new Dispute
        {
            OrderId = order.Id,
            BuyerId = order.BuyerId,
            SellerId = order.SellerId,
            Reason = reason,
            Description = description,
            Status = DisputeStatus.OPEN,
            CreatedAt = DateTime.UtcNow
        };
        db.Disputes.Add(dispute);
        await db.SaveChangesAsync(ct);

        return Accepted(new
        {
            dispute.Id,
            dispute.OrderId,
            dispute.BuyerId,
            dispute.SellerId,
            dispute.Reason,
            dispute.Description,
            status = dispute.Status.ToString(),
            dispute.CreatedAt,
            message = "Dispute filed. Escrow funds are frozen pending administrator review."
        });
    }

    [HttpPost("{id:long}/review")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Review(long id, CancellationToken ct)
    {
        var dispute = await db.Disputes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (dispute is null) return NotFound();
        if (dispute.Status != DisputeStatus.OPEN)
            return Conflict(new { message = "Only an open dispute can enter review." });

        dispute.Status = DisputeStatus.UNDER_REVIEW;
        dispute.ArbiterId = CurrentUserId;
        await db.SaveChangesAsync(ct);
        return Ok(new { dispute.Id, status = dispute.Status.ToString() });
    }

    [HttpPost("{id:long}/refund")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Refund(long id, DisputeDecisionInput input, CancellationToken ct)
        => await Resolve(id, input, refund: true, ct);

    [HttpPost("{id:long}/release")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Release(long id, DisputeDecisionInput input, CancellationToken ct)
        => await Resolve(id, input, refund: false, ct);

    private async Task<IActionResult> Resolve(long id, DisputeDecisionInput input, bool refund, CancellationToken ct)
    {
        var notes = input.Notes?.Trim();
        if (string.IsNullOrWhiteSpace(notes) || notes.Length < 5 || notes.Length > 1000)
            return BadRequest(new { message = "Decision notes must contain 5–1000 characters." });

        var dispute = await db.Disputes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (dispute is null) return NotFound();
        if (dispute.Status != DisputeStatus.UNDER_REVIEW)
            return Conflict(new { message = "Move the dispute into review before resolving it." });

        if (refund)
            await escrow.RefundFundsToBuyerAsync(dispute.OrderId, CurrentUserId, notes, ct);
        else
            await escrow.ReleaseFundsToMerchantAsync(dispute.OrderId, CurrentUserId, ct);

        dispute.Status = refund ? DisputeStatus.RESOLVED_REFUND : DisputeStatus.RESOLVED_RELEASE;
        dispute.ArbiterId = CurrentUserId;
        dispute.ArbiterDecisionNotes = notes;
        dispute.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { dispute.Id, status = dispute.Status.ToString(), dispute.ResolvedAt });
    }
}

public sealed record DisputeInput(long OrderId, string Reason, string Description);
public sealed record DisputeDecisionInput(string Notes);
