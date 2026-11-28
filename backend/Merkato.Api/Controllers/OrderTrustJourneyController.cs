using System.Security.Claims;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

/// <summary>
/// Read-only, participant-scoped timeline that joins existing order, payment,
/// receipt, delivery, return and dispute records. It never treats an uploaded
/// receipt or OCR result as proof that money was collected.
/// </summary>
[ApiController]
[Route("api/trust/orders")]
[Authorize(Roles = "BUYER,MERCHANT,ADMIN")]
public sealed class OrderTrustJourneyController : ControllerBase
{
    private readonly MerkatoDbContext _db;

    public OrderTrustJourneyController(MerkatoDbContext db) => _db = db;

    [HttpGet("{orderId:long}/journey")]
    public async Task<IActionResult> GetJourney(long orderId, CancellationToken ct)
    {
        if (orderId <= 0) return BadRequest(new { message = "A valid order ID is required." });
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var order = await _db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null) return NotFound(new { message = "Order not found." });

        var isAdmin = User.IsInRole("ADMIN");
        var isBuyer = order.BuyerId == userId;
        var isSeller = order.SellerId == userId;
        if (!isAdmin && !isBuyer && !isSeller) return Forbid();

        var seller = await _db.Users.AsNoTracking()
            .Where(x => x.Id == order.SellerId)
            .Select(x => new { x.Id, x.FullName, x.IsSellerVerified })
            .SingleOrDefaultAsync(ct);

        var payments = await _db.PaymentTransactions.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        var receipts = await _db.PaymentReceipts.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.Id, x.Status, x.CreatedAt, x.ReviewedAt, x.ReviewNote })
            .ToListAsync(ct);

        var delivery = await _db.Deliveries.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .Select(x => new { x.Id, x.DeliveryId, x.CourierType, x.Status, x.DispatchedAt, x.DeliveredAt, x.UpdatedAt })
            .SingleOrDefaultAsync(ct);

        var timeline = new List<TrustJourneyEvent>
        {
            new("ORDER_CREATED", "Order created", "The order was recorded by MERKATO.", order.CreatedAt, "MERKATO")
        };

        foreach (var payment in payments)
        {
            timeline.Add(new TrustJourneyEvent(
                "PAYMENT_" + payment.Status,
                "Payment status: " + payment.Status,
                payment.WebhookVerified
                    ? "The payment record has a verified provider callback."
                    : "This is the recorded payment state; it is not a provider confirmation unless marked verified.",
                payment.VerifiedAt ?? payment.CreatedAt,
                payment.WebhookVerified ? "VERIFIED_PROVIDER_CALLBACK" : "MERKATO"));
        }

        foreach (var receipt in receipts)
        {
            timeline.Add(new TrustJourneyEvent(
                "RECEIPT_" + receipt.Status,
                "Receipt " + receipt.Status.ToLowerInvariant().Replace('_', ' '),
                isBuyer || isAdmin
                    ? (string.IsNullOrWhiteSpace(receipt.ReviewNote) ? "Buyer-submitted payment evidence; a receipt alone does not confirm collection." : receipt.ReviewNote)
                    : "Buyer-submitted payment evidence is recorded for review.",
                receipt.CreatedAt,
                "MANUAL_REVIEW"));

            if (receipt.ReviewedAt.HasValue)
            {
                timeline.Add(new TrustJourneyEvent(
                    "RECEIPT_REVIEWED",
                    "Receipt review completed",
                    "Receipt review status: " + receipt.Status + ". This is separate from a verified payment-provider callback.",
                    receipt.ReviewedAt.Value,
                    "MERKATO_REVIEW"));
            }
        }

        if (delivery is not null)
        {
            timeline.Add(new TrustJourneyEvent(
                "DELIVERY_" + delivery.Status,
                "Delivery status: " + delivery.Status,
                "Courier mode: " + delivery.CourierType + ". Status is based on the delivery record.",
                delivery.UpdatedAt,
                "MERKATO"));

            if (delivery.DispatchedAt.HasValue)
                timeline.Add(new TrustJourneyEvent("DELIVERY_DISPATCHED", "Delivery dispatched", "The delivery record was marked as dispatched.", delivery.DispatchedAt.Value, "MERKATO"));
            if (delivery.DeliveredAt.HasValue)
                timeline.Add(new TrustJourneyEvent("DELIVERY_DELIVERED", "Delivery marked delivered", "The delivery record was marked delivered. Check the proof-of-delivery event where available.", delivery.DeliveredAt.Value, "MERKATO"));

            var tracking = await _db.DeliveryTrackingEvents.AsNoTracking()
                .Where(x => x.DeliveryId == delivery.Id)
                .OrderBy(x => x.OccurredAt)
                .ToListAsync(ct);

            foreach (var item in tracking)
            {
                timeline.Add(new TrustJourneyEvent(
                    "TRACKING_" + item.Status,
                    "Delivery update: " + item.Status,
                    item.Description,
                    item.OccurredAt,
                    item.Source));
            }
        }

        var returns = await _db.ReturnRequests.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
        foreach (var item in returns)
        {
            timeline.Add(new TrustJourneyEvent(
                "RETURN_" + item.Status,
                "Return status: " + item.Status,
                item.ResolutionNote ?? item.Reason,
                item.UpdatedAt,
                "MERKATO"));
        }

        var disputes = await _db.Disputes.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
        foreach (var item in disputes)
        {
            timeline.Add(new TrustJourneyEvent(
                "DISPUTE_" + item.Status,
                "Dispute status: " + item.Status,
                item.ArbiterDecisionNotes ?? item.Reason,
                item.ResolvedAt ?? item.CreatedAt,
                "MERKATO_REVIEW"));
        }

        return Ok(new
        {
            order = new
            {
                order.Id,
                order.OrderNumber,
                order.ProductTitle,
                order.Quantity,
                order.TotalAmountEtb,
                order.DeliveryFeeEtb,
                paymentMode = order.PaymentMode,
                paymentRail = order.PaymentRail.ToString(),
                escrowState = order.EscrowState.ToString(),
                deliveryStatus = order.DeliveryStatus.ToString(),
                order.CreatedAt
            },
            seller = seller is null ? null : new
            {
                seller.Id,
                seller.FullName,
                verificationStatus = seller.IsSellerVerified ? "MERKATO_REVIEWED" : "NOT_VERIFIED",
                verificationMeaning = "MERKATO internal seller review only; not Fayda or government identity verification."
            },
            payment = new
            {
                latestStatus = payments.LastOrDefault()?.Status.ToString() ?? "NOT_INITIATED",
                providerCallbackVerified = payments.Any(x => x.WebhookVerified),
                receiptCount = receipts.Count,
                note = "An uploaded receipt or OCR result is not proof of payment collection."
            },
            delivery = delivery is null ? null : new
            {
                delivery.DeliveryId,
                delivery.CourierType,
                status = delivery.Status.ToString(),
                delivery.DispatchedAt,
                delivery.DeliveredAt
            },
            timeline = timeline.OrderBy(x => x.OccurredAt).ToArray()
        });
    }

    public sealed record TrustJourneyEvent(
        string Code,
        string Title,
        string Description,
        DateTime OccurredAt,
        string Source);
}
