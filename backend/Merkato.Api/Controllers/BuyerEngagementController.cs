using System.Security.Claims;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Authorize(Roles = "BUYER")]
[Route("api/engagement")]
public sealed class BuyerEngagementController(MerkatoDbContext db) : ControllerBase
{
    private long BuyerId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("wishlist")]
    public async Task<IActionResult> Wishlist(CancellationToken ct)
    {
        var rows = await (from item in db.WishlistItems.AsNoTracking()
                          join product in db.Products.AsNoTracking() on item.ProductId equals product.Id
                          where item.BuyerId == BuyerId && product.IsActive
                          orderby item.CreatedAt descending
                          select new { item.Id, productId = product.Id, productName = product.Name, product.PriceEtb, product.Location, product.Category, product.ImageUrl, item.CreatedAt })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("wishlist/{productId:long}")]
    public async Task<IActionResult> SaveToWishlist(long productId, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId && p.IsActive, ct)) return NotFound("Active product not found.");
        if (await db.WishlistItems.AnyAsync(x => x.BuyerId == BuyerId && x.ProductId == productId, ct))
            return Conflict("Product is already in your wishlist.");
        var item = new WishlistItem { BuyerId = BuyerId, ProductId = productId };
        db.WishlistItems.Add(item);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Wishlist), new { }, new { item.Id, item.ProductId, item.CreatedAt });
    }

    [HttpDelete("wishlist/{productId:long}")]
    public async Task<IActionResult> RemoveFromWishlist(long productId, CancellationToken ct)
    {
        var item = await db.WishlistItems.FirstOrDefaultAsync(x => x.BuyerId == BuyerId && x.ProductId == productId, ct);
        if (item is null) return NoContent();
        db.WishlistItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("price-alerts")]
    public async Task<IActionResult> PriceAlerts(CancellationToken ct)
    {
        var rows = await db.PriceAlerts.AsNoTracking()
            .Where(x => x.BuyerId == BuyerId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.ProductId, productName = db.Products.Where(p => p.Id == x.ProductId).Select(p => p.Name).FirstOrDefault(), x.TargetPriceEtb, x.IsActive, x.CreatedAt, x.TriggeredAt })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("price-alerts")]
    public async Task<IActionResult> CreatePriceAlert(PriceAlertInput input, CancellationToken ct)
    {
        if (input.TargetPriceEtb <= 0 || input.TargetPriceEtb > 1_000_000_000m) return BadRequest("Target price must be a positive ETB amount.");
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.ProductId && p.IsActive, ct);
        if (product is null) return NotFound("Active product not found.");
        if (input.TargetPriceEtb >= product.PriceEtb) return BadRequest("Target price must be below the current product price.");
        if (await db.PriceAlerts.AnyAsync(x => x.BuyerId == BuyerId && x.ProductId == input.ProductId && x.IsActive, ct))
            return Conflict("An active price alert already exists for this product.");
        var alert = new PriceAlert { BuyerId = BuyerId, ProductId = input.ProductId, TargetPriceEtb = input.TargetPriceEtb };
        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(PriceAlerts), new { }, new { alert.Id, alert.ProductId, alert.TargetPriceEtb, alert.IsActive });
    }

    [HttpDelete("price-alerts/{id:long}")]
    public async Task<IActionResult> DeletePriceAlert(long id, CancellationToken ct)
    {
        var alert = await db.PriceAlerts.FirstOrDefaultAsync(x => x.Id == id && x.BuyerId == BuyerId, ct);
        if (alert is null) return NotFound();
        alert.IsActive = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
    {
        var query = db.UserNotifications.AsNoTracking().Where(x => x.UserId == BuyerId);
        if (unreadOnly) query = query.Where(x => !x.IsRead);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new { x.Id, x.Type, x.Title, x.Message, x.ActionUrl, x.IsRead, x.CreatedAt, x.ReadAt })
            .ToListAsync(ct);
        var unreadCount = await db.UserNotifications.CountAsync(x => x.UserId == BuyerId && !x.IsRead, ct);
        return Ok(new { unreadCount, notifications = rows });
    }

    [HttpPost("notifications/{id:long}/read")]
    public async Task<IActionResult> MarkNotificationRead(long id, CancellationToken ct)
    {
        var notification = await db.UserNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == BuyerId, ct);
        if (notification is null) return NotFound();
        if (!notification.IsRead) { notification.IsRead = true; notification.ReadAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
        return Ok(new { notification.Id, notification.IsRead, notification.ReadAt });
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken ct)
    {
        await db.UserNotifications.Where(x => x.UserId == BuyerId && !x.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAt, DateTime.UtcNow), ct);
        return NoContent();
    }

    [HttpDelete("notifications/{id:long}")]
    public async Task<IActionResult> DeleteNotification(long id, CancellationToken ct)
    {
        var row = await db.UserNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == BuyerId, ct);
        if (row is null) return NoContent();
        db.UserNotifications.Remove(row); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpGet("notification-preferences")]
    public async Task<IActionResult> GetNotificationPreferences(CancellationToken ct)
    {
        var row = await db.UserNotificationPreferences.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == BuyerId, ct);
        return Ok(row ?? new UserNotificationPreference { UserId = BuyerId });
    }

    [HttpPut("notification-preferences")]
    public async Task<IActionResult> UpdateNotificationPreferences(NotificationPreferencesInput input, CancellationToken ct)
    {
        var language = input.LanguageCode?.Trim().ToLowerInvariant();
        if (language is not "en" and not "om" and not "am") return BadRequest("LanguageCode must be en, om, or am.");
        var row = await db.UserNotificationPreferences.FirstOrDefaultAsync(x => x.UserId == BuyerId, ct);
        if (row is null) { row = new UserNotificationPreference { UserId = BuyerId }; db.UserNotificationPreferences.Add(row); }
        row.LanguageCode = language; row.InAppEnabled = input.InAppEnabled; row.EmailEnabled = input.EmailEnabled;
        row.SmsEnabled = input.SmsEnabled; row.MarketingEnabled = input.MarketingEnabled; row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { row.LanguageCode, row.InAppEnabled, row.EmailEnabled, row.SmsEnabled, row.MarketingEnabled, row.UpdatedAt });
    }

    public sealed record NotificationPreferencesInput(string LanguageCode, bool InAppEnabled, bool EmailEnabled, bool SmsEnabled, bool MarketingEnabled);
}

public sealed record PriceAlertInput(long ProductId, decimal TargetPriceEtb);
