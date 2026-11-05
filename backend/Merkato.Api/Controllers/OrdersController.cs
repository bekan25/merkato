using System.Security.Claims;
using System.Data;
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
                order.ProductTitle, order.Quantity, order.UnitPriceEtb, order.TotalAmountEtb, order.DeliveryFeeEtb,
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
            request.Quantity is < 1 or > 100000 ||
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

        if (request.ProductId is <= 0)
            return BadRequest(new { message = "ProductId must be a positive identifier when supplied." });

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.SellerId == request.SellerId &&
                item.IsActive &&
                (request.ProductId.HasValue
                    ? item.Id == request.ProductId.Value
                    : item.Name == request.ProductTitle), ct);

        if (product == null)
            return NotFound(new { message = "The selected product is no longer available." });

        var expectedProductAmount = product.PriceEtb * request.Quantity;
        if (request.AmountEtb != expectedProductAmount)
            return Conflict(new { message = "The product price or quantity total has changed. Refresh the marketplace and try again.", currentUnitPriceEtb = product.PriceEtb, quantity = request.Quantity, expectedProductAmountEtb = expectedProductAmount });

        await using var checkoutTransaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        PromotionCoupon? appliedCoupon = null;
        decimal discountEtb = 0m;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var couponCode = request.CouponCode.Trim().ToUpperInvariant();
            if (couponCode.Length > 40) return BadRequest(new { message = "Coupon code is too long." });
            appliedCoupon = await _db.PromotionCoupons.FirstOrDefaultAsync(x => x.Code == couponCode, ct);
            if (appliedCoupon is null) return Conflict(new { message = "Coupon code is invalid." });
            var now = DateTime.UtcNow;
            if (!appliedCoupon.IsActive || appliedCoupon.StartsAt > now || appliedCoupon.ExpiresAt <= now)
                return Conflict(new { message = "Coupon is not currently active." });
            if (appliedCoupon.AssignedUserId.HasValue && appliedCoupon.AssignedUserId.Value != buyerId)
                return Conflict(new { message = "This reward coupon belongs to another buyer account." });
            if (appliedCoupon.SellerId.HasValue && appliedCoupon.SellerId.Value != product.SellerId)
                return Conflict(new { message = "Coupon is valid only for products from its participating merchant." });
            if (request.AmountEtb < appliedCoupon.MinimumOrderEtb)
                return Conflict(new { message = "Coupon minimum order requirement is not met.", minimumOrderEtb = appliedCoupon.MinimumOrderEtb });
            var totalUses = await _db.CouponRedemptions.CountAsync(x => x.CouponId == appliedCoupon.Id, ct);
            var buyerUses = await _db.CouponRedemptions.CountAsync(x => x.CouponId == appliedCoupon.Id && x.BuyerId == buyerId, ct);
            if (appliedCoupon.UsageLimit.HasValue && totalUses >= appliedCoupon.UsageLimit.Value)
                return Conflict(new { message = "Coupon usage limit has been reached." });
            if (buyerUses >= appliedCoupon.PerUserLimit)
                return Conflict(new { message = "You have already used this coupon the maximum number of times." });
            discountEtb = PromotionsController.CalculateDiscount(appliedCoupon, request.AmountEtb);
        }

        var riskDecision = await _risk.EvaluateOrderRiskAsync(buyerId, request.AmountEtb - discountEtb + expectedDeliveryFee, ct);
        if (riskDecision != RiskDecision.ALLOW)
            return Conflict(new { message = "This order requires additional verification and cannot be placed yet.", riskDecision = riskDecision.ToString() });

        var reservationTime = DateTime.UtcNow;
        var reservedStock = await _db.ProductListingDetails
            .Where(x => x.ProductId == product.Id && x.StockQuantity - x.ReservedQuantity >= request.Quantity)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity + request.Quantity)
                .SetProperty(x => x.UpdatedAt, reservationTime), ct);
        if (reservedStock != 1)
            return Conflict(new { message = "This product is out of available stock or inventory has not been configured. Refresh the listing before checkout." });

        var order = new Order
        {
            OrderNumber = $"MK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            BuyerId = buyerId,
            SellerId = request.SellerId,
            ProductId = product.Id,
            Quantity = request.Quantity,
            UnitPriceEtb = product.PriceEtb,
            InventoryReservationState = InventoryReservationState.RESERVED,
            InventoryReservationExpiresAt = reservationTime.AddMinutes(15),
            ProductTitle = product.Name,
            TotalAmountEtb = request.AmountEtb - discountEtb + expectedDeliveryFee,
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

        if (appliedCoupon is not null)
        {
            _db.CouponRedemptions.Add(new CouponRedemption {
                CouponId = appliedCoupon.Id, BuyerId = buyerId, OrderId = order.Id,
                DiscountEtb = discountEtb, RedeemedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        await checkoutTransaction.CommitAsync(ct);

        var response = new OrderResponseDto(
            order.Id, order.OrderNumber, order.BuyerId, order.SellerId,
            order.ProductTitle, order.TotalAmountEtb, order.DeliveryFeeEtb,
            order.PaymentRail, order.DeliveryCourier, order.DeliveryAddress,
            order.EscrowState, order.DeliveryStatus, order.CreatedAt);

        return Ok(response);
    }


    [HttpPost("{orderId:long}/cancel")]
    [Authorize(Roles = "BUYER,ADMIN")]
    public async Task<IActionResult> CancelOrder(long orderId, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null) return NotFound();
        if (!User.IsInRole("ADMIN") && order.BuyerId != userId) return Forbid();
        if (order.EscrowState != EscrowState.AWAITING_PAYMENT)
            return Conflict(new { message = "Only an unpaid order can be cancelled." });

        var paymentPending = await _db.PaymentTransactions.AnyAsync(
            x => x.OrderId == orderId && (x.Status == PaymentStatus.INITIATED || x.Status == PaymentStatus.PENDING), ct);
        if (paymentPending)
            return Conflict(new { message = "A payment request is already pending. Wait for the provider result or contact support before cancelling." });

        if (order.InventoryReservationState == InventoryReservationState.RESERVED)
        {
            var claimed = await _db.Orders
                .Where(x => x.Id == orderId && x.InventoryReservationState == InventoryReservationState.RESERVED)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.InventoryReservationState, InventoryReservationState.RELEASED)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
            if (claimed == 1)
            {
                var released = await _db.ProductListingDetails
                    .Where(x => x.ProductId == order.ProductId && x.ReservedQuantity >= order.Quantity)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity - order.Quantity)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
                if (released != 1)
                    return Conflict(new { message = "Inventory reservation could not be safely released. Contact support." });
                order.InventoryReservationState = InventoryReservationState.RELEASED;
            }
            else
            {
                order.InventoryReservationState = await _db.Orders.AsNoTracking()
                    .Where(x => x.Id == orderId)
                    .Select(x => x.InventoryReservationState)
                    .FirstAsync(ct);
            }
        }

        order.EscrowState = EscrowState.CANCELLED;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { order.Id, order.OrderNumber, status = "CANCELLED" });
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
