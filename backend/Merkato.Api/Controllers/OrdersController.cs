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

    // Provisional MERKATO delivery estimates by destination city.
    // These are platform estimates, not live third-party courier tariffs.
    [HttpGet("delivery-fee")]
    [AllowAnonymous]
    public IActionResult QuoteDeliveryFee([FromQuery] string cityArea)
    {
        if (!TryGetDeliveryFee(cityArea, out var fee))
            return BadRequest(new { message = "Delivery is not yet available for this city. Contact MERKATO support for a quote." });

        return Ok(new
        {
            cityArea = cityArea.Trim(),
            feeEtb = fee,
            currency = "ETB",
            quoteType = "MERKATO_ESTIMATE",
            note = "Indicative platform fee. Final courier availability and charges are confirmed separately."
        });
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
            string.IsNullOrWhiteSpace(request.CityArea) ||
            request.AmountEtb <= 0 ||
            !Enum.IsDefined(request.PaymentRail))
            return BadRequest(new { message = "Provide a valid product, amount, payment method, delivery address and city." });

        if (!TryGetDeliveryFee(request.CityArea, out var expectedDeliveryFee))
            return BadRequest(new { message = "Delivery is not yet available for this city. Contact MERKATO support for a quote." });

        if (request.DeliveryFeeEtb != expectedDeliveryFee)
            return Conflict(new { message = "The delivery estimate has changed. Refresh the quote before placing the order.", deliveryFeeEtb = expectedDeliveryFee });

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

        var riskDecision = await _risk.EvaluateOrderRiskAsync(buyerId, request.AmountEtb + expectedDeliveryFee, ct);
        if (riskDecision != RiskDecision.ALLOW)
            return Conflict(new { message = "This order requires additional verification and cannot be placed yet.", riskDecision = riskDecision.ToString() });

        var order = new Order
        {
            OrderNumber = $"MK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            BuyerId = buyerId,
            SellerId = request.SellerId,
            ProductTitle = product.Name,
            TotalAmountEtb = request.AmountEtb + expectedDeliveryFee,
            DeliveryFeeEtb = expectedDeliveryFee,
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

    private static bool TryGetDeliveryFee(string? cityArea, out decimal fee)
    {
        fee = 0m;
        if (string.IsNullOrWhiteSpace(cityArea)) return false;
        var city = cityArea.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim().ToLowerInvariant();
        fee = city switch
        {
            "addis ababa" => 80m,
            "adama" => 150m,
            "hawassa" => 220m,
            "bahir dar" => 250m,
            "dire dawa" => 280m,
            "jimma" => 250m,
            "mekelle" => 300m,
            "gondar" => 280m,
            "dessie" => 260m,
            _ => 0m
        };
        return fee > 0m;
    }
}
