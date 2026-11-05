using System.Security.Claims;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/v1/wholesale")]
public sealed class WholesaleController(MerkatoDbContext db) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("products/{productId:long}/tiers")]
    public async Task<IActionResult> GetTiers(long productId, CancellationToken ct)
    {
        var exists = await db.Products.AnyAsync(p => p.Id == productId && p.IsActive, ct);
        if (!exists) return NotFound();
        var tiers = await db.WholesalePriceTiers.AsNoTracking()
            .Where(x => x.ProductId == productId)
            .OrderBy(x => x.MinimumQuantity)
            .Select(x => new { x.Id, x.MinimumQuantity, x.UnitPriceEtb })
            .ToListAsync(ct);
        return Ok(tiers);
    }

    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpPut("products/{productId:long}/tiers")]
    public async Task<IActionResult> SaveTiers(long productId, [FromBody] SaveTiersRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return NotFound();
        if (!User.IsInRole("ADMIN") && product.SellerId != userId) return Forbid();
        if (request.Tiers is null || request.Tiers.Count == 0 || request.Tiers.Any(t => t.MinimumQuantity < 2 || t.UnitPriceEtb <= 0) ||
            request.Tiers.Select(t => t.MinimumQuantity).Distinct().Count() != request.Tiers.Count)
            return BadRequest("Provide unique quantity tiers (minimum 2) with positive ETB prices.");

        var existing = await db.WholesalePriceTiers.Where(x => x.ProductId == productId).ToListAsync(ct);
        var submittedQuantities = request.Tiers.Select(t => t.MinimumQuantity).ToHashSet();
        db.WholesalePriceTiers.RemoveRange(existing.Where(x => !submittedQuantities.Contains(x.MinimumQuantity)));
        foreach (var tier in request.Tiers)
        {
            var row = existing.FirstOrDefault(x => x.MinimumQuantity == tier.MinimumQuantity);
            if (row is null)
            {
                db.WholesalePriceTiers.Add(new WholesalePriceTier { ProductId = productId, SellerId = product.SellerId, MinimumQuantity = tier.MinimumQuantity, UnitPriceEtb = tier.UnitPriceEtb });
            }
            else
            {
                row.UnitPriceEtb = tier.UnitPriceEtb;
                row.UpdatedAt = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
        return Ok(new { productId, count = request.Tiers.Count });
    }

    [Authorize(Roles = "BUYER")]
    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest([FromBody] CreateWholesaleRequest request, CancellationToken ct)
    {
        var buyerId = CurrentUserId();
        if (buyerId is null) return Unauthorized();
        if (request.Quantity < 2 || request.RequestedUnitPriceEtb < 0) return BadRequest("Quantity must be at least 2 and requested price cannot be negative.");
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive, ct);
        if (product is null) return NotFound("Product not found.");
        if (product.SellerId == buyerId) return BadRequest("You cannot request wholesale pricing for your own product.");
        var entity = new WholesaleRequest { RequestNumber = $"WH-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), ProductId = product.Id, BuyerId = buyerId.Value, SellerId = product.SellerId, Quantity = request.Quantity, RequestedUnitPriceEtb = request.RequestedUnitPriceEtb, BuyerNote = request.Note?.Trim() };
        db.WholesaleRequests.Add(entity);
        await db.SaveChangesAsync(ct);
        return Created($"/api/v1/wholesale/requests/{entity.Id}", new { entity.Id, entity.RequestNumber, entity.Status });
    }

    [Authorize(Roles = "BUYER")]
    [HttpGet("requests/mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var id = CurrentUserId();
        if (id is null) return Unauthorized();
        return Ok(await db.WholesaleRequests.AsNoTracking().Where(x => x.BuyerId == id)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct));
    }

    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpGet("requests/inbox")]
    public async Task<IActionResult> Inbox(CancellationToken ct)
    {
        var id = CurrentUserId();
        if (id is null) return Unauthorized();
        var query = db.WholesaleRequests.AsNoTracking();
        if (!User.IsInRole("ADMIN")) query = query.Where(x => x.SellerId == id);
        return Ok(await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct));
    }

    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpPost("requests/{id:long}/quote")]
    public async Task<IActionResult> Quote(long id, [FromBody] QuoteWholesaleRequest request, CancellationToken ct)
    {
        var sellerId = CurrentUserId();
        if (sellerId is null) return Unauthorized();
        if (request.UnitPriceEtb <= 0) return BadRequest("Quote price must be positive.");
        var entity = await db.WholesaleRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();
        if (!User.IsInRole("ADMIN") && entity.SellerId != sellerId) return Forbid();
        if (entity.Status != "REQUESTED") return Conflict("Only requested items can be quoted.");
        entity.QuotedUnitPriceEtb = request.UnitPriceEtb;
        entity.SellerNote = request.Note?.Trim();
        entity.Status = "QUOTED";
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { entity.Id, entity.Status, entity.QuotedUnitPriceEtb });
    }

    [Authorize(Roles = "BUYER")]
    [HttpPost("requests/{id:long}/accept")]
    public async Task<IActionResult> Accept(long id, CancellationToken ct)
    {
        var buyerId = CurrentUserId();
        if (buyerId is null) return Unauthorized();
        var entity = await db.WholesaleRequests.FirstOrDefaultAsync(x => x.Id == id && x.BuyerId == buyerId, ct);
        if (entity is null) return NotFound();
        if (entity.Status != "QUOTED" || entity.QuotedUnitPriceEtb is null) return Conflict("A seller quote is required.");
        entity.Status = "ACCEPTED";
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { entity.Id, entity.Status, entity.QuotedUnitPriceEtb, message = "Quote accepted. Complete payment through standard checkout." });
    }

    private long? CurrentUserId() => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

public sealed class SaveTiersRequest { public List<WholesaleTierInput> Tiers { get; set; } = []; }
public sealed class WholesaleTierInput { public int MinimumQuantity { get; set; } public decimal UnitPriceEtb { get; set; } }
public sealed class CreateWholesaleRequest { public long ProductId { get; set; } public int Quantity { get; set; } public decimal RequestedUnitPriceEtb { get; set; } public string? Note { get; set; } }
public sealed class QuoteWholesaleRequest { public decimal UnitPriceEtb { get; set; } public string? Note { get; set; } }
