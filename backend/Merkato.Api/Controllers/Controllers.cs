using Merkato.Application.DTOs;
using Merkato.Application.Interfaces;
using Merkato.Domain.Enums;
using Merkato.Domain.StateMachines;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Merkato.Api.Controllers;

// ================================================================
// DELIVERIES CONTROLLER
// ================================================================

[ApiController]
[Route("api/[controller]")]
public class DeliveriesController : ControllerBase
{
    private readonly IDeliveryService _deliveryService;
    private readonly Merkato.Infrastructure.Persistence.MerkatoDbContext _db;

    public DeliveriesController(
        IDeliveryService deliveryService,
        Merkato.Infrastructure.Persistence.MerkatoDbContext db)
    {
        _deliveryService = deliveryService;
        _db = db;
    }

    // ============================================================
    // CREATE DELIVERY
    // BUYER / MERCHANT / ADMIN
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "BUYER,MERCHANT,ADMIN")]
    public async Task<IActionResult> CreateDelivery(
        [FromBody] CreateDeliveryDto dto,
        CancellationToken ct)
    {
        if (dto.OrderId <= 0 || dto.FeeEtb < 0 ||
            string.IsNullOrWhiteSpace(dto.CourierType) ||
            string.IsNullOrWhiteSpace(dto.RawOtp) ||
            dto.RawOtp.Length < 4 || dto.RawOtp.Length > 8 ||
            !dto.RawOtp.All(char.IsDigit))
            return BadRequest(new { message = "Provide a valid order, courier, non-negative fee and 4–8 digit delivery OTP." });

        var order = await _db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dto.OrderId, ct);
        if (order == null) return NotFound(new { message = "Order not found." });

        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(claim, out var userId)) return Unauthorized();

        if (!User.IsInRole("ADMIN") &&
            !((User.IsInRole("BUYER") && order.BuyerId == userId) ||
              (User.IsInRole("MERCHANT") && order.SellerId == userId)))
            return Forbid();

        var delivery =
            await _deliveryService.CreateDeliveryAsync(
                dto.OrderId,
                dto.CourierType.Trim(),
                dto.FeeEtb,
                dto.RawOtp,
                ct);

        return Ok(new
        {
            deliveryId = delivery.DeliveryId,
            orderId = delivery.OrderId,
            status = delivery.Status.ToString(),
            courierType = delivery.CourierType,
            feeEtb = delivery.FeeEtb,
            createdAt = delivery.CreatedAt
        });
    }

    // Admin assigns a registered courier before courier-only actions are enabled.
    [HttpPost("{id}/assign-courier")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> AssignCourier(string id, [FromBody] AssignCourierRequest request, CancellationToken ct)
    {
        if (request.CourierUserId <= 0)
            return BadRequest(new { message = "A valid courier user ID is required." });

        var delivery = await _db.Deliveries.FirstOrDefaultAsync(x => x.DeliveryId == id, ct);
        if (delivery is null) return NotFound(new { message = "Delivery not found." });

        var courier = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CourierUserId && x.Role == UserRole.COURIER && !x.IsSuspended, ct);
        if (courier is null)
            return BadRequest(new { message = "The selected account is not an active courier." });

        if (delivery.Status != DeliveryStatus.CREATED)
            return Conflict(new { message = "Courier can only be assigned before pickup begins." });

        DeliveryStateMachine.ValidateTransition(delivery.Status, DeliveryStatus.ASSIGNED_COURIER);
        delivery.RiderName = courier.FullName;
        delivery.RiderPhone = courier.PhoneNumber;
        delivery.Status = DeliveryStatus.ASSIGNED_COURIER;
        delivery.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { deliveryId = delivery.DeliveryId, courierUserId = courier.Id,
            riderName = delivery.RiderName, riderPhone = delivery.RiderPhone, status = delivery.Status.ToString() });
    }

    // Buyer/merchant can see only their own delivery. A courier must be the assigned rider.
    [HttpGet("{id}")]
    [Authorize(Roles = "BUYER,MERCHANT,COURIER,ADMIN")]
    public async Task<IActionResult> GetDelivery(string id, CancellationToken ct)
    {
        var result = await _db.Deliveries.AsNoTracking()
            .Where(x => x.DeliveryId == id)
            .Join(_db.Orders.AsNoTracking(), delivery => delivery.OrderId, order => order.Id,
                (delivery, order) => new
                {
                    delivery.DeliveryId, delivery.OrderId, status = delivery.Status.ToString(),
                    delivery.CourierType, delivery.RiderName, delivery.RiderPhone,
                    delivery.FeeEtb, delivery.DispatchedAt, delivery.DeliveredAt,
                    delivery.CreatedAt, delivery.UpdatedAt, order.BuyerId, order.SellerId
                })
            .FirstOrDefaultAsync(ct);
        if (result == null) return NotFound(new { message = "Delivery not found." });

        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(claim, out var userId)) return Unauthorized();
        if (User.IsInRole("COURIER"))
        {
            var courierPhone = await _db.Users.AsNoTracking()
                .Where(x => x.Id == userId && x.Role == UserRole.COURIER && !x.IsSuspended)
                .Select(x => x.PhoneNumber).SingleOrDefaultAsync(ct);
            if (courierPhone is null || !string.Equals(courierPhone, result.RiderPhone, StringComparison.Ordinal))
                return Forbid();
        }
        else if (!User.IsInRole("ADMIN") &&
            !((User.IsInRole("BUYER") && result.BuyerId == userId) ||
              (User.IsInRole("MERCHANT") && result.SellerId == userId)))
            return Forbid();

        return Ok(new
        {
            result.DeliveryId, result.OrderId, result.status, result.CourierType,
            result.RiderName, result.RiderPhone, result.FeeEtb, result.DispatchedAt,
            result.DeliveredAt, result.CreatedAt, result.UpdatedAt
        });
    }

    // ============================================================
    // UPDATE DELIVERY STATUS
    // ONLY COURIER / ADMIN
    // ============================================================

    [HttpPost("{id}/status")]
    [Authorize(Roles = "COURIER,ADMIN")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromBody] DeliveryStatus nextStatus,
        CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var assignedPhone = await _db.Deliveries.AsNoTracking()
            .Where(x => x.DeliveryId == id)
            .Select(x => x.RiderPhone).SingleOrDefaultAsync(ct);
        if (assignedPhone is null) return NotFound(new { message = "Delivery not found or no courier assigned." });

        if (!User.IsInRole("ADMIN"))
        {
            var courierPhone = await _db.Users.AsNoTracking()
                .Where(x => x.Id == userId && x.Role == UserRole.COURIER && !x.IsSuspended)
                .Select(x => x.PhoneNumber).SingleOrDefaultAsync(ct);
            if (courierPhone is null || !string.Equals(courierPhone, assignedPhone, StringComparison.Ordinal))
                return Forbid();
        }

        var delivery =
            await _deliveryService.UpdateDeliveryStatusAsync(
                id,
                nextStatus,
                ct);

        return Ok(new
        {
            deliveryId = delivery.DeliveryId,
            orderId = delivery.OrderId,
            status = delivery.Status.ToString(),
            courierType = delivery.CourierType,
            feeEtb = delivery.FeeEtb,
            dispatchedAt = delivery.DispatchedAt,
            deliveredAt = delivery.DeliveredAt
        });
    }

    // ============================================================
    // VERIFY DELIVERY OTP
    // AUTHENTICATED USER
    // ============================================================

    [HttpPost("{id}/verify-otp")]
    [Authorize(Roles = "COURIER,ADMIN")]
    public async Task<IActionResult> VerifyOtp(
        string id,
        [FromBody] VerifyOtpDto dto,
        CancellationToken ct)
    {
        if (!string.Equals(
                id,
                dto.DeliveryId,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "DeliveryId in URL and request body must match."
            });
        }

        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!long.TryParse(
                userIdClaim,
                out var requesterId))
        {
            return Unauthorized(new
            {
                message = "Invalid authenticated user."
            });
        }

        var deliveryOrder = await _db.Deliveries
            .Where(x => x.DeliveryId == id)
            .Join(_db.Orders, delivery => delivery.OrderId, order => order.Id,
                (delivery, order) => new { order.BuyerId, order.SellerId, delivery.RiderPhone })
            .FirstOrDefaultAsync(ct);
        if (deliveryOrder == null) return NotFound(new { message = "Delivery not found." });

        if (!User.IsInRole("ADMIN"))
        {
            var courierPhone = await _db.Users.AsNoTracking()
                .Where(x => x.Id == requesterId && x.Role == UserRole.COURIER && !x.IsSuspended)
                .Select(x => x.PhoneNumber).SingleOrDefaultAsync(ct);
            if (courierPhone is null || !string.Equals(courierPhone, deliveryOrder.RiderPhone, StringComparison.Ordinal))
                return Forbid();
        }

        if (string.IsNullOrWhiteSpace(dto.OtpCode) || dto.OtpCode.Length < 4 ||
            dto.OtpCode.Length > 8 || !dto.OtpCode.All(char.IsDigit))
            return BadRequest(new { message = "OTP must contain 4–8 digits." });

        var delivery =
            await _deliveryService
                .VerifyOtpAndCompleteHandoverAsync(
                    id,
                    requesterId,
                    dto.OtpCode,
                    ct);

        return Ok(new
        {
            deliveryId = delivery.DeliveryId,
            orderId = delivery.OrderId,
            status = delivery.Status.ToString(),
            deliveredAt = delivery.DeliveredAt
        });
    }
}


// ================================================================
// INSPECTIONS CONTROLLER
// ================================================================

[ApiController]
[Route("api/[controller]")]
public class InspectionsController : ControllerBase
{
    private readonly IInspectionService _inspectionService;

    public InspectionsController(
        IInspectionService inspectionService)
    {
        _inspectionService = inspectionService;
    }

    // ============================================================
    // BUYER ACCEPTS INSPECTION
    // ============================================================

    [HttpPost("{orderId}/accept")]
    [Authorize(Roles = "BUYER,ADMIN")]
    public async Task<IActionResult> AcceptInspection(
        long orderId)
    {
        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!long.TryParse(
                userIdClaim,
                out var buyerId))
        {
            return Unauthorized(new
            {
                message = "Invalid authenticated user."
            });
        }

        var inspection =
            await _inspectionService
                .AcceptInspectionAsync(
                    orderId,
                    buyerId);

        return Ok(new
        {
            id = inspection.Id,
            orderId = inspection.OrderId,
            status = inspection.Status.ToString(),
            startedAt = inspection.StartedAt,
            expiresAt = inspection.ExpiresAt,
            completedAt = inspection.CompletedAt
        });
    }
}


// ================================================================
// DISPUTES CONTROLLER
// ================================================================

[ApiController]
[Route("api/[controller]")]
public class DisputesController : ControllerBase
{
    private readonly Merkato.Infrastructure.Persistence.MerkatoDbContext _db;
    private readonly IEscrowService _escrow;

    public DisputesController(
        Merkato.Infrastructure.Persistence.MerkatoDbContext db,
        IEscrowService escrow)
    {
        _db = db;
        _escrow = escrow;
    }

    // ============================================================
    // FILE DISPUTE
    // BUYER ONLY
    // ============================================================

    [HttpPost("{orderId}")]
    [Authorize(Roles = "BUYER")]
    public async Task<IActionResult> FileDispute(
        long orderId,
        [FromBody] FileDisputeRequest request,
        CancellationToken ct)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                message = "Request body is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new
            {
                message = "Dispute reason is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new
            {
                message = "Dispute description is required."
            });
        }

        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!long.TryParse(
                userIdClaim,
                out var buyerId))
        {
            return Unauthorized(new
            {
                message = "Invalid authenticated user."
            });
        }

        var order =
            await _db.Orders
                .FindAsync(
                    new object[] { orderId },
                    ct);

        if (order == null)
        {
            return NotFound(new
            {
                message =
                    $"Order {orderId} not found."
            });
        }

        // Buyer must own the order.
        if (order.BuyerId != buyerId)
        {
            return Forbid();
        }

        // Only escrow-protected orders can be disputed.
        if (order.EscrowState != EscrowState.HELD &&
            order.EscrowState != EscrowState.INSPECTION_ACTIVE)
        {
            return Conflict(new
            {
                message =
                    $"Order cannot be disputed from escrow state {order.EscrowState}."
            });
        }

        // Prevent duplicate open dispute.
        var existing =
            await _db.Disputes
                .AnyAsync(
                    d =>
                        d.OrderId == orderId &&
                        (
                            d.Status == DisputeStatus.OPEN ||
                            d.Status == DisputeStatus.UNDER_REVIEW
                        ),
                    ct);

        if (existing)
        {
            return Conflict(new
            {
                message =
                    "An active dispute already exists for this order."
            });
        }

        // Create dispute.
        var dispute = new Merkato.Domain.Entities.Dispute
        {
            OrderId = order.Id,
            BuyerId = order.BuyerId,
            SellerId = order.SellerId,
            Reason = request.Reason.Trim(),
            Description = request.Description.Trim(),
            Status = DisputeStatus.OPEN,
            CreatedAt = DateTime.UtcNow
        };

        _db.Disputes.Add(dispute);

        await _db.SaveChangesAsync(ct);

        // Move escrow to DISPUTED.
        await _escrow.FileDisputeAsync(
            orderId,
            buyerId,
            request.Reason,
            ct);

        return Ok(new
        {
            id = dispute.Id,
            orderId = dispute.OrderId,
            buyerId = dispute.BuyerId,
            sellerId = dispute.SellerId,
            reason = dispute.Reason,
            description = dispute.Description,
            status = dispute.Status.ToString(),
            createdAt = dispute.CreatedAt
        });
    }


    // ============================================================
    // GET SINGLE DISPUTE
    // BUYER / MERCHANT / ARBITER / ADMIN
    // ============================================================

    [HttpGet("{id}")]
    [Authorize(Roles = "BUYER,MERCHANT,ARBITER,ADMIN")]
    public async Task<IActionResult> GetDispute(
        long id,
        CancellationToken ct)
    {
        var dispute =
            await _db.Disputes
                .FirstOrDefaultAsync(
                    d => d.Id == id,
                    ct);

        if (dispute == null)
        {
            return NotFound(new
            {
                message =
                    $"Dispute {id} not found."
            });
        }

        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        long.TryParse(
            userIdClaim,
            out var userId);

        var role =
            User.FindFirstValue(
                ClaimTypes.Role);

        // Buyer/Merchant may only see their own dispute.
        if (role == "BUYER" &&
            dispute.BuyerId != userId)
        {
            return Forbid();
        }

        if (role == "MERCHANT" &&
            dispute.SellerId != userId)
        {
            return Forbid();
        }

        return Ok(new
        {
            id = dispute.Id,
            orderId = dispute.OrderId,
            buyerId = dispute.BuyerId,
            sellerId = dispute.SellerId,
            reason = dispute.Reason,
            description = dispute.Description,
            status = dispute.Status.ToString(),
            arbiterId = dispute.ArbiterId,
            decisionNotes =
                dispute.ArbiterDecisionNotes,
            createdAt = dispute.CreatedAt,
            resolvedAt = dispute.ResolvedAt
        });
    }


    // ============================================================
    // LIST DISPUTES
    // ARBITER / ADMIN ONLY
    // ============================================================

    [HttpGet]
    [Authorize(Roles = "ARBITER,ADMIN")]
    public async Task<IActionResult> GetDisputes(
        CancellationToken ct)
    {
        var disputes =
            await _db.Disputes
                .OrderByDescending(
                    d => d.CreatedAt)
                .Select(d => new
                {
                    id = d.Id,
                    orderId = d.OrderId,
                    buyerId = d.BuyerId,
                    sellerId = d.SellerId,
                    reason = d.Reason,
                    description = d.Description,
                    status = d.Status.ToString(),
                    arbiterId = d.ArbiterId,
                    decisionNotes =
                        d.ArbiterDecisionNotes,
                    createdAt = d.CreatedAt,
                    resolvedAt = d.ResolvedAt
                })
                .ToListAsync(ct);

        return Ok(disputes);
    }


    // ============================================================
    // MOVE DISPUTE TO UNDER REVIEW
    // ARBITER / ADMIN ONLY
    // ============================================================

    [HttpPost("{id}/review")]
    [Authorize(Roles = "ARBITER,ADMIN")]
    public async Task<IActionResult> StartReview(
        long id,
        CancellationToken ct)
    {
        var dispute =
            await _db.Disputes
                .FirstOrDefaultAsync(
                    d => d.Id == id,
                    ct);

        if (dispute == null)
        {
            return NotFound(new
            {
                message =
                    $"Dispute {id} not found."
            });
        }

        if (dispute.Status != DisputeStatus.OPEN)
        {
            return Conflict(new
            {
                message =
                    $"Dispute cannot enter review from {dispute.Status}."
            });
        }

        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!long.TryParse(
                userIdClaim,
                out var arbiterId))
        {
            return Unauthorized();
        }

        dispute.ArbiterId = arbiterId;
        dispute.Status = DisputeStatus.UNDER_REVIEW;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            id = dispute.Id,
            orderId = dispute.OrderId,
            status = dispute.Status.ToString(),
            arbiterId = dispute.ArbiterId
        });
    }


    // ============================================================
    // RESOLVE → REFUND BUYER
    // ARBITER / ADMIN ONLY
    // ============================================================

    [HttpPost("{id}/refund")]
    [Authorize(Roles = "ARBITER,ADMIN")]
    public async Task<IActionResult> Refund(
        long id,
        [FromBody] ResolveDisputeRequest request,
        CancellationToken ct)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Notes))
        {
            return BadRequest(new
            {
                message =
                    "Resolution notes are required."
            });
        }

        var dispute =
            await _db.Disputes
                .FirstOrDefaultAsync(
                    d => d.Id == id,
                    ct);

        if (dispute == null)
        {
            return NotFound(new
            {
                message =
                    $"Dispute {id} not found."
            });
        }

        if (dispute.Status != DisputeStatus.OPEN &&
            dispute.Status != DisputeStatus.UNDER_REVIEW)
        {
            return Conflict(new
            {
                message =
                    $"Dispute is already {dispute.Status}."
            });
        }

        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!long.TryParse(
                userIdClaim,
                out var arbiterId))
        {
            return Unauthorized();
        }

        var escrow =
            await _escrow
                .RefundFundsToBuyerAsync(
                    dispute.OrderId,
                    arbiterId,
                    request.Notes,
                    ct);

        dispute.ArbiterId = arbiterId;
        dispute.ArbiterDecisionNotes =
            request.Notes.Trim();

        dispute.Status =
            DisputeStatus.RESOLVED_REFUND;

        dispute.ResolvedAt =
            DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            disputeId = dispute.Id,
            orderId = dispute.OrderId,
            status = dispute.Status.ToString(),
            escrowState = escrow.Status.ToString(),
            refundedAmountEtb =
                escrow.RefundedAmountEtb,
            resolvedAt = dispute.ResolvedAt
        });
    }


    // ============================================================
    // RESOLVE → RELEASE TO MERCHANT
    // ARBITER / ADMIN ONLY
    // ============================================================

    [HttpPost("{id}/release")]
    [Authorize(Roles = "ARBITER,ADMIN")]
    public async Task<IActionResult> Release(
        long id,
        [FromBody] ResolveDisputeRequest request,
        CancellationToken ct)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Notes))
        {
            return BadRequest(new
            {
                message =
                    "Resolution notes are required."
            });
        }

        var dispute =
            await _db.Disputes
                .FirstOrDefaultAsync(
                    d => d.Id == id,
                    ct);

        if (dispute == null)
        {
            return NotFound(new
            {
                message =
                    $"Dispute {id} not found."
            });
        }

        if (dispute.Status != DisputeStatus.OPEN &&
            dispute.Status != DisputeStatus.UNDER_REVIEW)
        {
            return Conflict(new
            {
                message =
                    $"Dispute is already {dispute.Status}."
            });
        }

        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!long.TryParse(
                userIdClaim,
                out var arbiterId))
        {
            return Unauthorized();
        }

        var escrow =
            await _escrow
                .ReleaseFundsToMerchantAsync(
                    dispute.OrderId,
                    arbiterId,
                    ct);

        dispute.ArbiterId = arbiterId;
        dispute.ArbiterDecisionNotes =
            request.Notes.Trim();

        dispute.Status =
            DisputeStatus.RESOLVED_RELEASE;

        dispute.ResolvedAt =
            DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            disputeId = dispute.Id,
            orderId = dispute.OrderId,
            status = dispute.Status.ToString(),
            escrowState = escrow.Status.ToString(),
            releasedAmountEtb =
                escrow.ReleasedAmountEtb,
            resolvedAt = dispute.ResolvedAt
        });
    }
}


// ================================================================
// REQUEST MODELS
// ================================================================

public record FileDisputeRequest(
    string Reason,
    string Description
);

public record ResolveDisputeRequest(
    string Notes
);
public sealed record AssignCourierRequest(long CourierUserId);
