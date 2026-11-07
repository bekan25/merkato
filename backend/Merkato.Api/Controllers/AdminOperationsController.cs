using System.Security.Claims;
using System.Text.Json;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

/// <summary>
/// Administrative operational queues. Every mutation is role-gated and recorded
/// in the existing append-only audit log.
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin/operations")]
public sealed class AdminOperationsController(MerkatoDbContext db) : ControllerBase
{
    private long ActorId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? q, [FromQuery] string? role,
        [FromQuery] bool? suspended, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(x => x.FullName.ToLower().Contains(term) ||
                x.PhoneNumber.ToLower().Contains(term) || (x.Email != null && x.Email.ToLower().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var parsedRole))
            query = query.Where(x => x.Role == parsedRole);
        if (suspended.HasValue) query = query.Where(x => x.IsSuspended == suspended.Value);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.FullName, x.PhoneNumber, x.Email, role = x.Role.ToString(),
                x.IsSuspended, x.IsSellerVerified, x.IsFaydaVerified, x.TrustScore, x.CreatedAt, x.UpdatedAt })
            .ToListAsync(ct);
        return Ok(new { items = rows, total, page, pageSize });
    }

    [HttpPost("users/{id:long}/suspension")]
    public async Task<IActionResult> SetSuspension(long id, [FromBody] AdminUserSuspensionInput input,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length < 5 || input.Reason.Trim().Length > 500)
            return BadRequest(new { message = "A reason of 5–500 characters is required." });
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return NotFound();
        if (user.Id == ActorId) return Conflict(new { message = "An administrator cannot suspend their own account." });
        if (user.Role == UserRole.ADMIN) return Conflict(new { message = "Admin accounts cannot be suspended from this queue. Use the controlled administrator access procedure." });
        if (user.IsSuspended == input.Suspend) return Conflict(new { message = input.Suspend ? "Account is already suspended." : "Account is not suspended." });

        user.IsSuspended = input.Suspend;
        user.TokenVersion++;
        user.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(CreateAudit("USER_" + (input.Suspend ? "SUSPENDED" : "REINSTATED"),
            "User", user.Id.ToString(), new { reason = input.Reason.Trim() }));
        await db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.IsSuspended, user.TokenVersion, user.UpdatedAt });
    }

    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] string? q, [FromQuery] bool? active,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = from product in db.Products.AsNoTracking()
                    join seller in db.Users.AsNoTracking() on product.SellerId equals seller.Id
                    select new { product, seller.FullName, seller.PhoneNumber };
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(x => x.product.Name.ToLower().Contains(term) ||
                x.product.Category.ToLower().Contains(term) || x.FullName.ToLower().Contains(term));
        }
        if (active.HasValue) query = query.Where(x => x.product.IsActive == active.Value);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.product.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.product.Id, x.product.SellerId, sellerName = x.FullName,
                sellerPhone = x.PhoneNumber, x.product.Name, x.product.PriceEtb, x.product.Category,
                x.product.Location, x.product.ImageUrl, x.product.IsActive, x.product.CreatedAt, x.product.UpdatedAt })
            .ToListAsync(ct);
        return Ok(new { items = rows, total, page, pageSize });
    }

    [HttpPost("products/{id:long}/visibility")]
    public async Task<IActionResult> SetProductVisibility(long id, [FromBody] AdminProductVisibilityInput input,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length < 5 || input.Reason.Trim().Length > 500)
            return BadRequest(new { message = "A reason of 5–500 characters is required." });
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product is null) return NotFound();
        if (product.IsActive == input.Active) return Conflict(new { message = input.Active ? "Product is already active." : "Product is already hidden." });
        product.IsActive = input.Active;
        product.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(CreateAudit(input.Active ? "PRODUCT_RESTORED" : "PRODUCT_HIDDEN",
            "Product", product.Id.ToString(), new { reason = input.Reason.Trim(), sellerId = product.SellerId }));
        await db.SaveChangesAsync(ct);
        return Ok(new { product.Id, product.IsActive, product.UpdatedAt });
    }

    [HttpGet("returns")]
    public async Task<IActionResult> Returns([FromQuery] string? status, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.ReturnRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReturnStatus>(status, true, out var parsed))
            query = query.Where(x => x.Status == parsed);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.OrderId, x.BuyerId, x.SellerId, x.Reason, x.EvidenceUrl,
                x.ResolutionNote, status = x.Status.ToString(), x.CreatedAt, x.UpdatedAt })
            .ToListAsync(ct);
        return Ok(new { items = rows, total, page, pageSize });
    }

    [HttpPost("returns/{id:long}/decision")]
    public async Task<IActionResult> DecideReturn(long id, [FromBody] AdminReturnDecisionInput input,
        CancellationToken ct)
    {
        var note = input.Note?.Trim() ?? string.Empty;
        if (note.Length < 5 || note.Length > 1000)
            return BadRequest(new { message = "A decision note of 5–1000 characters is required." });
        if (!Enum.TryParse<ReturnStatus>(input.Decision, true, out var decision) ||
            decision is not (ReturnStatus.APPROVED or ReturnStatus.REJECTED or ReturnStatus.RECEIVED))
            return BadRequest(new { message = "Decision must be APPROVED, REJECTED or RECEIVED. Actual refunds must use the verified payment/escrow process." });
        var row = await db.ReturnRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return NotFound();
        if (row.Status is ReturnStatus.REFUNDED or ReturnStatus.REJECTED)
            return Conflict(new { message = "This return request is already closed." });
        if (decision == ReturnStatus.RECEIVED && row.Status != ReturnStatus.APPROVED)
            return Conflict(new { message = "A return must be approved before it can be marked received." });
        if (decision == ReturnStatus.APPROVED && row.Status != ReturnStatus.REQUESTED)
            return Conflict(new { message = "Only requested returns can be approved." });
        if (decision == ReturnStatus.REJECTED && row.Status != ReturnStatus.REQUESTED)
            return Conflict(new { message = "Only requested returns can be rejected." });

        row.Status = decision;
        row.ResolutionNote = note;
        row.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(CreateAudit("RETURN_" + decision, "ReturnRequest", row.Id.ToString(),
            new { orderId = row.OrderId, note }));
        await db.SaveChangesAsync(ct);
        return Ok(new { row.Id, status = row.Status.ToString(), row.ResolutionNote, row.UpdatedAt });
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs([FromQuery] string? entityType, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(x => x.EntityType == entityType.Trim());
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.EventId, x.ActorId, x.ActorRole, x.Action, x.EntityType,
                x.EntityId, x.Result, x.MetadataJson, x.IpAddress, x.TraceId, x.CreatedAt })
            .ToListAsync(ct);
        return Ok(new { items = rows, total, page, pageSize });
    }

    private AuditLog CreateAudit(string action, string entityType, string entityId, object metadata) => new()
    {
        ActorId = ActorId,
        ActorRole = "ADMIN",
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        UserAgent = Request.Headers.UserAgent.ToString(),
        TraceId = HttpContext.TraceIdentifier,
        Result = "SUCCESS",
        MetadataJson = JsonSerializer.Serialize(metadata),
        CreatedAt = DateTime.UtcNow
    };
}

public sealed record AdminUserSuspensionInput(bool Suspend, string Reason);
public sealed record AdminProductVisibilityInput(bool Active, string Reason);
public sealed record AdminReturnDecisionInput(string Decision, string? Note);
