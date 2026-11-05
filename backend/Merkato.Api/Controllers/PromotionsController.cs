using System.Data;
using System.Security.Claims;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/promotions")]
public sealed class PromotionsController(MerkatoDbContext db) : ControllerBase
{
    private long CurrentUserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [AllowAnonymous]
    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await db.PromotionCoupons.AsNoTracking()
            .Where(x => x.IsActive && x.StartsAt <= now && x.ExpiresAt > now)
            .OrderBy(x => x.ExpiresAt)
            .Select(x => new { x.Title, x.DiscountType, x.DiscountValue, x.MaximumDiscountEtb, x.MinimumOrderEtb, x.ExpiresAt, merchantPromotion = x.SellerId != null })
            .Take(20).ToListAsync(ct);
        return Ok(rows);
    }

    [Authorize(Roles = "BUYER,ADMIN")]
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(CouponValidationInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Code) || input.Code.Length > 40 || input.ProductId <= 0)
            return BadRequest("A valid coupon code and product are required.");
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.ProductId && x.IsActive, ct);
        if (product is null) return NotFound("Product not found.");
        var coupon = await db.PromotionCoupons.AsNoTracking().FirstOrDefaultAsync(x => x.Code == input.Code.Trim().ToUpperInvariant(), ct);
        if (coupon is null) return NotFound("Coupon code is invalid.");
        if (coupon.AssignedUserId.HasValue && coupon.AssignedUserId.Value != CurrentUserId) return Conflict("This reward coupon belongs to another account.");
        var error = await ValidateCoupon(coupon, CurrentUserId, product.SellerId, product.PriceEtb, ct);
        if (error is not null) return Conflict(error);
        return Ok(new { coupon.Code, coupon.Title, discountEtb = CalculateDiscount(coupon, product.PriceEtb), subtotalEtb = product.PriceEtb, totalEtb = product.PriceEtb - CalculateDiscount(coupon, product.PriceEtb), coupon.ExpiresAt });
    }

    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateCouponInput input, CancellationToken ct)
    {
        var code = (input.Code ?? string.Empty).Trim().ToUpperInvariant();
        var title = (input.Title ?? string.Empty).Trim();
        var type = (input.DiscountType ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length is < 4 or > 40 || !code.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-') || title.Length is < 3 or > 120)
            return BadRequest("Coupon code must be 4-40 letters, numbers or hyphens; title must be 3-120 characters.");
        if (type is not ("PERCENT" or "FIXED") || input.DiscountValue <= 0 ||
            (type == "PERCENT" && input.DiscountValue > 90) ||
            (type == "FIXED" && input.DiscountValue > 1_000_000) ||
            input.MinimumOrderEtb < 0 || (input.MaximumDiscountEtb.HasValue && input.MaximumDiscountEtb.Value < 0) ||
            input.UsageLimit is < 1 or > 1_000_000 || input.PerUserLimit is < 1 or > 10 ||
            input.StartsAt.Kind != DateTimeKind.Utc || input.ExpiresAt.Kind != DateTimeKind.Utc ||
            input.StartsAt >= input.ExpiresAt || input.ExpiresAt <= DateTime.UtcNow || input.ExpiresAt > DateTime.UtcNow.AddDays(180))
            return BadRequest("Invalid discount, limits or UTC campaign dates. Campaign duration cannot exceed 180 days.");
        long? sellerId = null;
        if (User.IsInRole("MERCHANT"))
        {
            sellerId = CurrentUserId;
            if (input.SellerId.HasValue && input.SellerId.Value != sellerId.Value) return Forbid();
        }
        else if (input.SellerId.HasValue)
        {
            if (!await db.Users.AnyAsync(x => x.Id == input.SellerId.Value && x.Role == Merkato.Domain.Enums.UserRole.MERCHANT, ct))
                return BadRequest("Seller account does not exist.");
            sellerId = input.SellerId.Value;
        }
        if (await db.PromotionCoupons.AnyAsync(x => x.Code == code, ct)) return Conflict("Coupon code already exists.");
        var coupon = new PromotionCoupon {
            Code = code, Title = title, DiscountType = type, DiscountValue = input.DiscountValue,
            MaximumDiscountEtb = input.MaximumDiscountEtb, MinimumOrderEtb = input.MinimumOrderEtb,
            SellerId = sellerId, UsageLimit = input.UsageLimit, PerUserLimit = input.PerUserLimit,
            StartsAt = input.StartsAt, ExpiresAt = input.ExpiresAt, CreatedBy = CurrentUserId
        };
        db.PromotionCoupons.Add(coupon);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Active), new { }, new { coupon.Id, coupon.Code, coupon.Title, coupon.DiscountType, coupon.DiscountValue, coupon.StartsAt, coupon.ExpiresAt, coupon.SellerId });
    }

    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var query = db.PromotionCoupons.AsNoTracking();
        if (!User.IsInRole("ADMIN")) query = query.Where(x => x.SellerId == CurrentUserId);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Select(x => new {
            x.Id, x.Code, x.Title, x.DiscountType, x.DiscountValue, x.MinimumOrderEtb,
            x.MaximumDiscountEtb, x.UsageLimit, x.PerUserLimit, x.StartsAt, x.ExpiresAt, x.IsActive,
            usedCount = db.CouponRedemptions.Count(r => r.CouponId == x.Id)
        }).ToListAsync(ct);
        return Ok(rows);
    }

    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpPost("{id:long}/deactivate")]
    public async Task<IActionResult> Deactivate(long id, CancellationToken ct)
    {
        var coupon = await db.PromotionCoupons.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (coupon is null) return NotFound();
        if (!User.IsInRole("ADMIN") && coupon.SellerId != CurrentUserId) return Forbid();
        coupon.IsActive = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<string?> ValidateCoupon(PromotionCoupon coupon, long buyerId, long sellerId, decimal subtotal, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (!coupon.IsActive || coupon.StartsAt > now || coupon.ExpiresAt <= now) return "This coupon is not currently active.";
        if (coupon.SellerId.HasValue && coupon.SellerId.Value != sellerId) return "This coupon is valid only for products from its participating merchant.";
        if (subtotal < coupon.MinimumOrderEtb) return $"Minimum eligible order is {coupon.MinimumOrderEtb:0.00} ETB.";
        if (coupon.UsageLimit.HasValue && await db.CouponRedemptions.CountAsync(x => x.CouponId == coupon.Id, ct) >= coupon.UsageLimit.Value) return "This coupon has reached its usage limit.";
        if (await db.CouponRedemptions.CountAsync(x => x.CouponId == coupon.Id && x.BuyerId == buyerId, ct) >= coupon.PerUserLimit) return "You have already used this coupon the maximum number of times.";
        return null;
    }

    internal static decimal CalculateDiscount(PromotionCoupon coupon, decimal subtotal)
    {
        var amount = coupon.DiscountType == "PERCENT" ? subtotal * coupon.DiscountValue / 100m : coupon.DiscountValue;
        if (coupon.MaximumDiscountEtb.HasValue) amount = Math.Min(amount, coupon.MaximumDiscountEtb.Value);
        return Math.Round(Math.Min(subtotal, amount), 2, MidpointRounding.AwayFromZero);
    }
}

public sealed record CouponValidationInput(string Code, long ProductId);
public sealed record CreateCouponInput(string Code, string Title, string DiscountType, decimal DiscountValue, decimal? MaximumDiscountEtb, decimal MinimumOrderEtb, int? UsageLimit, int PerUserLimit, DateTime StartsAt, DateTime ExpiresAt, long? SellerId = null);
