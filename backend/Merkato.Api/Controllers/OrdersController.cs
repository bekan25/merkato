using System.Security.Claims;
using Merkato.Application.DTOs;
using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly MerkatoDbContext _db;
    private readonly IRiskAssessmentService _risk;

    public OrdersController(MerkatoDbContext db, IRiskAssessmentService risk)
    {
        _db = db;
        _risk = risk;
    }

    [HttpGet("mine")]
    [Authorize(Roles = "BUYER,ADMIN")]
    public async Task<IActionResult> GetMyOrders(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var query = _db.Orders.AsNoTracking();
        if (!User.IsInRole("ADMIN"))
            query = query.Where(order => order.BuyerId == userId);

        var orders = await query
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => new OrderResponseDto(
                order.Id, order.OrderNumber, order.BuyerId, order.SellerId,
                order.ProductTitle, order.TotalAmountEtb, order.DeliveryFeeEtb,
                order.PaymentRail, order.DeliveryCourier, order.DeliveryAddress,
                order.EscrowState, order.DeliveryStatus, order.CreatedAt))
            .ToListAsync(ct);

        return Ok(orders);
    }

    [HttpGet("merchant")]
    [Authorize(Roles = "MERCHANT,ADMIN")]
    public async Task<IActionResult> GetMerchantOrders(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var query = _db.Orders.AsNoTracking();
        if (!User.IsInRole("ADMIN"))
            query = query.Where(order => order.SellerId == userId);

        var orders = await query
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => new OrderResponseDto(
                order.Id, order.OrderNumber, order.BuyerId, order.SellerId,
                order.ProductTitle, order.TotalAmountEtb, order.DeliveryFeeEtb,
                order.PaymentRail, order.DeliveryCourier, order.DeliveryAddress,
                order.EscrowState, order.DeliveryStatus, order.CreatedAt))
            .ToListAsync(ct);

        return Ok(orders);
    }

    [HttpPost]
    [Authorize(Roles = "BUYER,ADMIN")]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder(
        [FromBody] CreateOrderDto request,
        CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var buyerId))
            return Unauthorized();

        if (request.SellerId <= 0 ||
            string.IsNullOrWhiteSpace(request.ProductTitle) ||
            string.IsNullOrWhiteSpace(request.DeliveryAddress) ||
            request.AmountEtb <= 0 ||
            request.DeliveryFeeEtb < 0 ||
            !Enum.IsDefined(request.PaymentRail))
            return BadRequest(new { message = "Provide a valid product, amount, payment method and delivery address." });

        var seller = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == request.SellerId, ct);

        if (seller == null || seller.Role != UserRole.MERCHANT)
            return BadRequest(new { message = "The selected seller is not an active merchant account." });

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.SellerId == request.SellerId &&
                item.IsActive &&
                item.Name == request.ProductTitle, ct);

        if (product == null)
            return NotFound(new { message = "The selected product is no longer available." });

        if (request.AmountEtb != product.PriceEtb)
            return Conflict(new { message = "The product price has changed. Refresh the marketplace and try again.", currentPriceEtb = product.PriceEtb });

        var riskDecision = await _risk.EvaluateOrderRiskAsync(buyerId, request.AmountEtb + request.DeliveryFeeEtb, ct);
        if (riskDecision != RiskDecision.ALLOW)
            return Conflict(new { message = "This order requires additional verification and cannot be placed yet.", riskDecision = riskDecision.ToString() });

        var order = new Order
        {
            OrderNumber = $"MK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            BuyerId = buyerId,
            SellerId = request.SellerId,
            ProductTitle = product.Name,
            TotalAmountEtb = request.AmountEtb + request.DeliveryFeeEtb,
            DeliveryFeeEtb = request.DeliveryFeeEtb,
            PaymentRail = request.PaymentRail,
            DeliveryCourier = string.IsNullOrWhiteSpace(request.DeliveryCourier) ? "BEU_DELIVERY" : request.DeliveryCourier.Trim(),
            DeliveryAddress = request.DeliveryAddress.Trim(),
            CityArea = string.IsNullOrWhiteSpace(request.CityArea) ? "Addis Ababa" : request.CityArea.Trim(),
            EscrowState = EscrowState.AWAITING_PAYMENT,
            DeliveryStatus = DeliveryStatus.CREATED,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        var response = new OrderResponseDto(
            order.Id, order.OrderNumber, order.BuyerId, order.SellerId,
            order.ProductTitle, order.TotalAmountEtb, order.DeliveryFeeEtb,
            order.PaymentRail, order.DeliveryCourier, order.DeliveryAddress,
            order.EscrowState, order.DeliveryStatus, order.CreatedAt);

        return Ok(response);
    }
}
