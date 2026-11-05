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

    public PaymentsController(
        MerkatoDbContext db,
        IPaymentService paymentService)
    {
        _db = db;
        _paymentService = paymentService;
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