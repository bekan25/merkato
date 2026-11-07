using Merkato.Application.DTOs;
using Merkato.Application.Interfaces;
using Merkato.Domain.Enums;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly MerkatoDbContext _db;
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _configuration;

    public PaymentsController(
        MerkatoDbContext db,
        IPaymentService paymentService,
        IConfiguration configuration)
    {
        _db = db;
        _paymentService = paymentService;
        _configuration = configuration;
    }

    private decimal CommissionRate(string key, decimal fallback)
    {
        var configured = _configuration.GetValue<decimal?>(key) ?? fallback;
        if (configured < 0m || configured > 25m)
            throw new InvalidOperationException($"{key} must be between 0 and 25 percent.");
        return configured;
    }

    // =====================================================
    // INITIATE PAYMENT
    // =====================================================

    [HttpPost("initiate")]
    [Authorize(Roles = "BUYER,ADMIN")]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiatePaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
        {
            return BadRequest(new
            {
                error = "Idempotency-Key header is required."
            });
        }

        var buyerIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(buyerIdClaim, out var buyerId))
        {
            return Unauthorized();
        }

        var order = await _db.Orders
            .FirstOrDefaultAsync(
                o => o.Id == request.OrderId,
                ct);

        if (order == null)
        {
            return NotFound(new
            {
                error = "Order not found."
            });
        }

        if (order.BuyerId != buyerId &&
            !User.IsInRole("ADMIN"))
        {
            return Forbid();
        }

        if (order.EscrowState != EscrowState.AWAITING_PAYMENT)
        {
            return Conflict(new
            {
                error = "Order is not awaiting payment.",
                escrowState = order.EscrowState.ToString()
            });
        }

        if (order.InventoryReservationState is InventoryReservationState.RELEASED or InventoryReservationState.EXPIRED ||
            (order.InventoryReservationState == InventoryReservationState.RESERVED &&
             order.InventoryReservationExpiresAt <= DateTime.UtcNow))
            return Conflict(new { error = "The inventory reservation has expired. Create a new order before paying." });

        var expectedAmount = order.TotalAmountEtb;

        if (!Enum.IsDefined(request.PaymentRail))
            return BadRequest(new { error = "Unsupported payment rail." });

        if (request.AmountEtb != expectedAmount)
        {
            return BadRequest(new
            {
                error = "Payment amount does not match order total.",
                expectedAmount,
                suppliedAmount = request.AmountEtb
            });
        }

        if (order.PaymentMode == "SELLER_DIRECT")
        {
            if (request.PaymentRail != order.PaymentRail)
                return BadRequest(new { error = "Payment method must match the seller's configured destination." });

            var existingDirect = await _db.PaymentTransactions
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);
            if (existingDirect is not null)
            {
                if (existingDirect.OrderId != order.Id || existingDirect.AmountEtb != expectedAmount)
                    return Conflict(new { error = "Idempotency key was already used for another payment request." });
                return Ok(new
                {
                    paymentTransactionId = existingDirect.Id,
                    orderId = existingDirect.OrderId,
                    amountEtb = existingDirect.AmountEtb,
                    currency = existingDirect.Currency,
                    paymentRail = existingDirect.PaymentRail.ToString(),
                    status = existingDirect.Status.ToString(),
                    idempotencyKey = existingDirect.IdempotencyKey,
                    transferMode = "SELLER_DIRECT",
                    message = "Transfer directly to the seller's account, then upload your receipt."
                });
            }

            var directTransaction = new PaymentTransaction
            {
                OrderId = order.Id,
                IdempotencyKey = idempotencyKey,
                PaymentRail = order.PaymentRail,
                AmountEtb = expectedAmount,
                Currency = "ETB",
                Status = PaymentStatus.PENDING,
                WebhookVerified = false,
                CreatedAt = DateTime.UtcNow
            };
            _db.PaymentTransactions.Add(directTransaction);
            await _db.SaveChangesAsync(ct);
            return Ok(new
            {
                paymentTransactionId = directTransaction.Id,
                orderId = directTransaction.OrderId,
                amountEtb = directTransaction.AmountEtb,
                currency = directTransaction.Currency,
                paymentRail = directTransaction.PaymentRail.ToString(),
                status = directTransaction.Status.ToString(),
                idempotencyKey = directTransaction.IdempotencyKey,
                transferMode = "SELLER_DIRECT",
                message = "Transfer directly to the seller's account, then upload your receipt."
            });
        }

        PaymentTransaction transaction;
        try
        {
            transaction = await _paymentService.InitiatePaymentAsync(
                order.Id, idempotencyKey, expectedAmount, request.PaymentRail, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        return Ok(new
        {
            paymentTransactionId = transaction.Id,
            orderId = transaction.OrderId,
            amountEtb = transaction.AmountEtb,
            currency = transaction.Currency,
            paymentRail = transaction.PaymentRail.ToString(),
            status = transaction.Status.ToString(),
            idempotencyKey = transaction.IdempotencyKey,
            createdAt = transaction.CreatedAt
        });
    }

    // =====================================================
    // PAYMENT WEBHOOK
    // =====================================================

    [HttpPost("webhooks/{provider}")]
    [AllowAnonymous]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> ProcessWebhook(
        string provider,
        [FromBody] JsonElement payload,
        [FromHeader(Name = "X-Signature-SHA256")] string? signature,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return Unauthorized(new
            {
                error = "Missing X-Signature-SHA256 header."
            });
        }

        // Signature verification must use the exact JSON bytes received from the provider.
        var rawPayload = payload.GetRawText();
        PaymentWebhookDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<PaymentWebhookDto>(
                rawPayload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "Invalid webhook payload." });
        }

        if (dto is null)
            return BadRequest(new { error = "Webhook payload is required." });

        var directOrder = await _db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderNumber == dto.OrderNumber, ct);
        if (directOrder?.PaymentMode == "SELLER_DIRECT")
            return Conflict(new { error = "Seller-direct transfers are confirmed by the seller after checking their own account; provider webhook processing is not used for this payment mode." });

        try
        {
            var transaction =
                await _paymentService.ProcessWebhookAsync(
                    provider,
                    dto,
                    rawPayload,
                    signature,
                    ct);

            // Referral reward is earned only after a provider-verified payment, never on a click or signup alone.
            if (transaction.Status == PaymentStatus.VERIFIED)
            {
                var paidOrder = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == transaction.OrderId, ct);
                if (paidOrder is not null)
                {
                    // Attribute a verified purchase to the buyer's most recent approved creator post click
                    // for the same product within 30 days. A unique OrderId index makes webhook retries safe.
                    if (paidOrder.ProductId.HasValue && !await _db.CreatorCommissions.AnyAsync(x => x.OrderId == paidOrder.Id, ct))
                    {
                        var attributionSince = DateTime.UtcNow.AddDays(-30);
                        var attributedPost = await (
                            from click in _db.SocialCommerceClicks.AsNoTracking()
                            join post in _db.SocialCommercePosts.AsNoTracking() on click.PostId equals post.Id
                            where click.ViewerId == paidOrder.BuyerId
                                && click.CreatedAt >= attributionSince
                                && post.ProductId == paidOrder.ProductId.Value
                                && post.Status == "APPROVED"
                                && post.CreatorId != paidOrder.BuyerId
                            orderby click.CreatedAt descending
                            select post
                        ).FirstOrDefaultAsync(ct);

                        if (attributedPost is not null)
                        {
                            var eligibleAmount = Math.Max(0m, paidOrder.ProductSubtotalEtb - paidOrder.DiscountEtb);
                            var rate = CommissionRate("Commissions:CreatorRatePercent", 5m);
                            _db.CreatorCommissions.Add(new CreatorCommission
                            {
                                CreatorId = attributedPost.CreatorId,
                                OrderId = paidOrder.Id,
                                PostId = attributedPost.Id,
                                EligibleAmountEtb = eligibleAmount,
                                RatePercent = rate,
                                CommissionEtb = Math.Round(eligibleAmount * rate / 100m, 2, MidpointRounding.AwayFromZero),
                                Status = "HELD",
                                CreatedAt = DateTime.UtcNow
                            });
                            try { await _db.SaveChangesAsync(ct); }
                            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation }) { _db.Entry(_db.CreatorCommissions.Local.First(x => x.OrderId == paidOrder.Id)).State = EntityState.Detached; }
                        }
                    }

                    // Affiliate commission is attributed only to a registered referral with an active affiliate profile.
                    // It is held until escrow release; a unique OrderId index protects webhook retries.
                    if (paidOrder.ProductId.HasValue && paidOrder.UnitPriceEtb > 0m && paidOrder.Quantity > 0 &&
                        !await _db.AffiliateCommissions.AnyAsync(x => x.OrderId == paidOrder.Id, ct))
                    {
                        var affiliateReferral = await _db.AffiliateReferrals
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.ReferredUserId == paidOrder.BuyerId &&
                                x.RegisteredAt != null && x.AffiliateUserId != paidOrder.BuyerId, ct);
                        if (affiliateReferral is not null &&
                            await _db.AffiliateProfiles.AnyAsync(x => x.UserId == affiliateReferral.AffiliateUserId && x.IsActive, ct))
                        {
                            var affiliateRatePercent = CommissionRate("Commissions:AffiliateRatePercent", 2m);
                            var eligibleAmount = Math.Max(0m, paidOrder.ProductSubtotalEtb - paidOrder.DiscountEtb);
                            _db.AffiliateCommissions.Add(new AffiliateCommission
                            {
                                AffiliateUserId = affiliateReferral.AffiliateUserId,
                                OrderId = paidOrder.Id,
                                EligibleAmountEtb = eligibleAmount,
                                RatePercent = affiliateRatePercent,
                                CommissionEtb = Math.Round(eligibleAmount * affiliateRatePercent / 100m, 2, MidpointRounding.AwayFromZero),
                                Status = "HELD",
                                CreatedAt = DateTime.UtcNow
                            });
                            try { await _db.SaveChangesAsync(ct); }
                            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
                            {
                                var pending = _db.AffiliateCommissions.Local.FirstOrDefault(x => x.OrderId == paidOrder.Id);
                                if (pending is not null) _db.Entry(pending).State = EntityState.Detached;
                            }
                        }
                    }

                    var referral = await _db.AffiliateReferrals.FirstOrDefaultAsync(x => x.ReferredUserId == paidOrder.BuyerId, ct);
                    if (referral is not null && !await _db.PromotionCoupons.AnyAsync(x => x.SourceReferralId == referral.Id, ct))
                    {
                        var reward = new PromotionCoupon {
                            Code = "REFER" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4)),
                            Title = "MERKATO referral thank-you reward",
                            DiscountType = "FIXED",
                            DiscountValue = 50m,
                            MinimumOrderEtb = 200m,
                            UsageLimit = 1,
                            PerUserLimit = 1,
                            AssignedUserId = referral.AffiliateUserId,
                            SourceReferralId = referral.Id,
                            StartsAt = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.AddDays(30),
                            CreatedBy = referral.AffiliateUserId
                        };
                        _db.PromotionCoupons.Add(reward);
                        try { await _db.SaveChangesAsync(ct); }
                        catch (DbUpdateException) { _db.Entry(reward).State = EntityState.Detached; }
                    }
                }
            }

            return Ok(new
            {
                paymentTransactionId = transaction.Id,
                orderId = transaction.OrderId,
                status = transaction.Status.ToString(),
                webhookVerified = transaction.WebhookVerified,
                railTransactionId = transaction.RailTransactionId,
                verifiedAt = transaction.VerifiedAt
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new
            {
                error = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                error = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                error = ex.Message
            });
        }
    }
}

// =====================================================
// REQUEST DTO
// =====================================================

public record InitiatePaymentRequest(
    long OrderId,
    decimal AmountEtb,
    PaymentRail PaymentRail
);