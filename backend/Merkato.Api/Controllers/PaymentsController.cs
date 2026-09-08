using Merkato.Application.DTOs;
using Merkato.Application.Interfaces;
using Merkato.Domain.Enums;
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
        if (string.IsNullOrWhiteSpace(idempotencyKey))
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

        if (request.AmountEtb != expectedAmount)
        {
            return BadRequest(new
            {
                error = "Payment amount does not match order total.",
                expectedAmount,
                suppliedAmount = request.AmountEtb
            });
        }

        var transaction =
            await _paymentService.InitiatePaymentAsync(
                order.Id,
                idempotencyKey,
                expectedAmount,
                request.PaymentRail,
                ct);

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
    public async Task<IActionResult> ProcessWebhook(
        string provider,
        [FromBody] PaymentWebhookDto dto,
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

        var rawPayload =
    JsonSerializer.Serialize(dto, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

        Console.WriteLine("======================================");
        Console.WriteLine("WEBHOOK DEBUG");
        Console.WriteLine($"Provider: {provider}");
        Console.WriteLine($"Raw Payload: {rawPayload}");
        Console.WriteLine($"Received Signature: {signature}");
        Console.WriteLine("======================================");

        try
        {
            var transaction =
                await _paymentService.ProcessWebhookAsync(
                    provider,
                    dto,
                    rawPayload,
                    signature,
                    ct);

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