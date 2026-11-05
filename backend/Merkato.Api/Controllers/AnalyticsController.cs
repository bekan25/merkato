using System.Security.Claims;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public sealed class AnalyticsController : ControllerBase
{
    private readonly MerkatoDbContext _db;
    public AnalyticsController(MerkatoDbContext db) => _db = db;

    [HttpGet("merchant")]
    [Authorize(Roles = "MERCHANT,ADMIN")]
    public async Task<IActionResult> Merchant(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var sellerId = userId;
        if (User.IsInRole("ADMIN") && long.TryParse(Request.Query["sellerId"], out var requestedSeller))
            sellerId = requestedSeller;

        var products = _db.Products.AsNoTracking().Where(p => p.SellerId == sellerId);
        var orders = _db.Orders.AsNoTracking().Where(o => o.SellerId == sellerId);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var recentOrders = await orders.OrderByDescending(o => o.CreatedAt).Take(10)
            .Select(o => new { o.Id, o.OrderNumber, o.ProductTitle, o.TotalAmountEtb, o.DeliveryStatus, o.EscrowState, o.CreatedAt })
            .ToListAsync(ct);

        return Ok(new
        {
            productCount = await products.CountAsync(ct),
            activeProductCount = await products.CountAsync(p => p.IsActive, ct),
            orderCount = await orders.CountAsync(ct),
            monthlyOrderCount = await orders.CountAsync(o => o.CreatedAt >= monthStart, ct),
            grossOrderValueEtb = await orders.SumAsync(o => (decimal?)o.TotalAmountEtb, ct) ?? 0m,
            monthlyGrossOrderValueEtb = await orders.Where(o => o.CreatedAt >= monthStart).SumAsync(o => (decimal?)o.TotalAmountEtb, ct) ?? 0m,
            recentOrders
        });
    }

    [HttpGet("admin")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Admin(CancellationToken ct)
    {
        var users = _db.Users.AsNoTracking();
        var orders = _db.Orders.AsNoTracking();
        var products = _db.Products.AsNoTracking();
        var disputes = _db.Disputes.AsNoTracking();

        var recentUsers = await users.OrderByDescending(u => u.CreatedAt).Take(8)
            .Select(u => new { u.Id, u.FullName, u.PhoneNumber, role = u.Role.ToString(), u.CreatedAt })
            .ToListAsync(ct);

        return Ok(new
        {
            totalUsers = await users.CountAsync(ct),
            buyerCount = await users.CountAsync(u => u.Role == UserRole.BUYER, ct),
            merchantCount = await users.CountAsync(u => u.Role == UserRole.MERCHANT, ct),
            totalProducts = await products.CountAsync(ct),
            activeProducts = await products.CountAsync(p => p.IsActive, ct),
            totalOrders = await orders.CountAsync(ct),
            grossOrderValueEtb = await orders.SumAsync(o => (decimal?)o.TotalAmountEtb, ct) ?? 0m,
            openDisputes = await disputes.CountAsync(d => d.Status.ToString() == "OPEN" || d.Status.ToString() == "UNDER_REVIEW", ct),
            recentUsers
        });
    }
}
