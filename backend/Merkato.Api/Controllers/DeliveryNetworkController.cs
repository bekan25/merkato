using System.Security.Claims;
using System.Text.Json;
using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Domain.StateMachines;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/v1/delivery")]
public sealed class DeliveryNetworkController : ControllerBase
{
    private readonly MerkatoDbContext _db;
    private readonly IDeliveryService? _deliveryService;
    public DeliveryNetworkController(MerkatoDbContext db, IDeliveryService? deliveryService = null)
    {
        _db = db;
        _deliveryService = deliveryService;
    }

    private long? CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("providers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        var providers = await _db.DeliveryProviders.AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.ProviderKind).ThenBy(x => x.DisplayName)
            .Select(x => new { x.Code, x.DisplayName, x.ProviderKind, x.SupportsQuotes, x.SupportsBooking, x.SupportsTracking })
            .ToListAsync(ct);
        return Ok(providers);
    }

    [HttpPost("partners/apply")]
    [Authorize]
    public async Task<IActionResult> Apply([FromBody] DeliveryPartnerApplication request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var applicant = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (applicant is null || applicant.IsSuspended) return Forbid();
        var areas = (request.ServiceAreas ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
        if (areas.Length == 0 || string.IsNullOrWhiteSpace(request.TransportMode) ||
            (request.TransportMode?.Length ?? 0) > 30 || request.VehicleDescription?.Length > 200 ||
            request.CapacityKg is < 0 or > 10000)
            return BadRequest(new { message = "Provide at least one service area, a transport mode and valid vehicle capacity." });

        var existing = await _db.DeliveryPartnerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (existing is not null)
        {
            if (existing.ApprovalStatus is "APPROVED" or "PENDING")
                return Conflict(new { message = "A delivery partner application is already active." });
            existing.ApprovalStatus = "PENDING";
            existing.ServiceAreasJson = JsonSerializer.Serialize(areas);
            existing.TransportMode = request.TransportMode.Trim();
            existing.VehicleDescription = request.VehicleDescription?.Trim();
            existing.CapacityKg = request.CapacityKg;
            existing.ReviewNote = null;
            existing.ReviewerUserId = null;
            existing.AppliedAt = DateTime.UtcNow;
            existing.ReviewedAt = null;
        }
        else
        {
            _db.DeliveryPartnerProfiles.Add(new DeliveryPartnerProfile
            {
                UserId = userId.Value,
                ServiceAreasJson = JsonSerializer.Serialize(areas),
                TransportMode = request.TransportMode.Trim(),
                VehicleDescription = request.VehicleDescription?.Trim(),
                CapacityKg = request.CapacityKg
            });
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { status = "PENDING", message = "Application submitted for administrator review." });
    }

    [HttpGet("partners/available")]
    [Authorize(Roles = "MERCHANT,ADMIN")]
    public async Task<IActionResult> AvailablePartners([FromQuery] string city, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(city) || city.Length > 100)
            return BadRequest(new { message = "Provide a valid delivery city or area." });
        var candidates = await _db.DeliveryPartnerProfiles.AsNoTracking()
            .Where(x => x.ApprovalStatus == "APPROVED")
            .Join(_db.Users.AsNoTracking(), profile => profile.UserId, user => user.Id,
                (profile, user) => new { profile, user.FullName, user.IsSuspended })
            .Where(x => !x.IsSuspended)
            .ToListAsync(ct);
        var eligible = candidates.Where(x =>
            (JsonSerializer.Deserialize<string[]>(x.profile.ServiceAreasJson) ?? [])
                .Contains(city.Trim(), StringComparer.OrdinalIgnoreCase))
            .Select(x => new { userId = x.profile.UserId, name = x.FullName, x.profile.TransportMode, x.profile.VehicleDescription, x.profile.CapacityKg })
            .ToList();
        return Ok(eligible);
    }

    [HttpGet("partners/mine")]
    [Authorize]
    public async Task<IActionResult> MyApplication(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var profile = await _db.DeliveryPartnerProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (profile is null) return NotFound();
        return Ok(new { profile.Id, profile.ApprovalStatus, serviceAreas = JsonSerializer.Deserialize<string[]>(profile.ServiceAreasJson) ?? [], profile.TransportMode, profile.VehicleDescription, profile.CapacityKg, profile.AppliedAt, profile.ReviewedAt, profile.ReviewNote });
    }

    [HttpGet("admin/partners")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> ReviewQueue([FromQuery] string status = "PENDING", CancellationToken ct = default)
    {
        var profiles = await _db.DeliveryPartnerProfiles.AsNoTracking()
            .Where(x => x.ApprovalStatus == status)
            .OrderBy(x => x.AppliedAt)
            .Select(x => new { x.Id, x.UserId, x.ApprovalStatus, x.ServiceAreasJson, x.TransportMode, x.VehicleDescription, x.CapacityKg, x.AppliedAt })
            .ToListAsync(ct);
        return Ok(profiles.Select(x => new { x.Id, x.UserId, x.ApprovalStatus, serviceAreas = JsonSerializer.Deserialize<string[]>(x.ServiceAreasJson) ?? [], x.TransportMode, x.VehicleDescription, x.CapacityKg, x.AppliedAt }));
    }

    [HttpPost("admin/partners/{profileId:long}/review")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Review(long profileId, [FromBody] DeliveryPartnerReview request, CancellationToken ct)
    {
        var reviewerId = CurrentUserId;
        if (reviewerId is null) return Unauthorized();
        var status = request.Status?.Trim().ToUpperInvariant();
        if (status is not ("APPROVED" or "REJECTED" or "SUSPENDED") || request.Note?.Length > 1000)
            return BadRequest(new { message = "Status must be APPROVED, REJECTED or SUSPENDED." });
        var profile = await _db.DeliveryPartnerProfiles.FirstOrDefaultAsync(x => x.Id == profileId, ct);
        if (profile is null) return NotFound();
        var applicant = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == profile.UserId, ct);
        if (applicant is null || applicant.IsSuspended) return Conflict(new { message = "Suspended or unavailable accounts cannot become delivery partners." });
        if (status == "APPROVED" && !applicant.IsFaydaVerified)
            return Conflict(new { message = "Fayda identity verification is required before approving an individual delivery partner." });
        profile.ApprovalStatus = status;
        profile.ReviewerUserId = reviewerId;
        profile.ReviewNote = request.Note?.Trim();
        profile.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { profile.Id, profile.ApprovalStatus, profile.ReviewedAt });
    }

    [HttpGet("deliveries/dispatch-queue")]
    [Authorize(Roles = "MERCHANT,ADMIN")]
    public async Task<IActionResult> DispatchQueue(CancellationToken ct)
    {
        var actorId = CurrentUserId;
        if (actorId is null) return Unauthorized();
        var query = from delivery in _db.Deliveries.AsNoTracking()
                    join order in _db.Orders.AsNoTracking() on delivery.OrderId equals order.Id
                    where delivery.Status == DeliveryStatus.CREATED &&
                        !_db.DeliveryAssignments.Any(a => a.DeliveryId == delivery.Id && (a.Status == "OFFERED" || a.Status == "ACCEPTED"))
                    select new { delivery.Id, delivery.OrderId, order.OrderNumber, order.SellerId, order.CityArea, order.DeliveryAddress, delivery.FeeEtb, order.DeliveryCourier };
        if (!User.IsInRole("ADMIN")) query = query.Where(x => x.SellerId == actorId.Value);
        var rows = await query.OrderBy(x => x.OrderNumber).ToListAsync(ct);
        return Ok(rows.Select(x => new { deliveryId = x.Id, x.OrderId, x.OrderNumber, x.CityArea, x.DeliveryAddress, x.FeeEtb, selectedMode = x.DeliveryCourier }));
    }

    [HttpGet("deliveries/seller-active")]
    [Authorize(Roles = "MERCHANT,ADMIN")]
    public async Task<IActionResult> SellerManagedDeliveries(CancellationToken ct)
    {
        var actorId = CurrentUserId;
        if (actorId is null) return Unauthorized();
        var query = from assignment in _db.DeliveryAssignments.AsNoTracking()
                    join provider in _db.DeliveryProviders.AsNoTracking() on assignment.ProviderId equals provider.Id
                    join delivery in _db.Deliveries.AsNoTracking() on assignment.DeliveryId equals delivery.Id
                    join order in _db.Orders.AsNoTracking() on delivery.OrderId equals order.Id
                    where provider.Code == "SELLER" && assignment.Status == "ACCEPTED" && delivery.Status != DeliveryStatus.DELIVERED
                    select new { AssignmentId = assignment.Id, DeliveryRecordId = assignment.DeliveryId, PublicDeliveryId = delivery.DeliveryId, AssignmentStatus = assignment.Status, DeliveryStatus = delivery.Status, FeeEtb = delivery.FeeEtb, order.OrderNumber, order.CityArea, order.DeliveryAddress, order.SellerId };
        if (!User.IsInRole("ADMIN")) query = query.Where(x => x.SellerId == actorId.Value);
        return Ok(await query.OrderBy(x => x.OrderNumber).ToListAsync(ct));
    }

    [HttpPost("assignments/{assignmentId:long}/seller-status")]
    [Authorize(Roles = "MERCHANT,ADMIN")]
    public async Task<IActionResult> UpdateSellerManagedStatus(long assignmentId, [FromBody] DeliveryAssignmentStatusRequest request, CancellationToken ct)
    {
        var actorId = CurrentUserId;
        if (actorId is null) return Unauthorized();
        if (_deliveryService is null) return StatusCode(503);
        var assignment = await _db.DeliveryAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId && x.Status == "ACCEPTED", ct);
        if (assignment is null) return NotFound();
        var provider = await _db.DeliveryProviders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignment.ProviderId && x.Code == "SELLER", ct);
        var delivery = await _db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignment.DeliveryId, ct);
        if (provider is null || delivery is null) return NotFound();
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == delivery.OrderId, ct);
        if (order is null || (!User.IsInRole("ADMIN") && (order.SellerId != actorId || assignment.AssignedByUserId != actorId)))
            return NotFound();
        if (!Enum.TryParse<DeliveryStatus>(request.Status, true, out var nextStatus) ||
            nextStatus is DeliveryStatus.CREATED or DeliveryStatus.ASSIGNED_COURIER or DeliveryStatus.DELIVERED)
            return BadRequest(new { message = "Choose a valid delivery progress status. Completion requires buyer OTP." });
        try
        {
            var updated = await _deliveryService.UpdateDeliveryStatusAsync(delivery.DeliveryId, nextStatus, ct);
            _db.DeliveryTrackingEvents.Add(new DeliveryTrackingEvent { DeliveryId = delivery.Id, AssignmentId = assignment.Id, Source = "MERKATO", EventKey = $"seller-status:{assignment.Id}:{nextStatus}:{updated.RowVersion}", Status = nextStatus.ToString(), Description = $"Seller updated delivery status to {nextStatus}." });
            await _db.SaveChangesAsync(ct);
            return Ok(new { updated.DeliveryId, status = updated.Status.ToString(), updated.UpdatedAt });
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("assignments/{assignmentId:long}/seller-verify-otp")]
    [Authorize(Roles = "MERCHANT,ADMIN")]
    public async Task<IActionResult> VerifySellerManagedOtp(long assignmentId, [FromBody] DeliveryAssignmentOtpRequest request, CancellationToken ct)
    {
        var actorId = CurrentUserId;
        if (actorId is null) return Unauthorized();
        if (_deliveryService is null) return StatusCode(503);
        var assignment = await _db.DeliveryAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId && x.Status == "ACCEPTED", ct);
        if (assignment is null) return NotFound();
        var provider = await _db.DeliveryProviders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignment.ProviderId && x.Code == "SELLER", ct);
        var delivery = await _db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignment.DeliveryId, ct);
        if (provider is null || delivery is null) return NotFound();
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == delivery.OrderId, ct);
        if (order is null || (!User.IsInRole("ADMIN") && (order.SellerId != actorId || assignment.AssignedByUserId != actorId)))
            return NotFound();
        if (string.IsNullOrWhiteSpace(request.OtpCode) || request.OtpCode.Length is < 4 or > 8 || !request.OtpCode.All(char.IsDigit))
            return BadRequest(new { message = "OTP must contain 4–8 digits." });
        try
        {
            var completed = await _deliveryService.VerifyOtpAndCompleteHandoverAsync(delivery.DeliveryId, actorId.Value, request.OtpCode, ct);
            assignment.Status = "COMPLETED";
            assignment.CompletedAt = DateTime.UtcNow;
            _db.DeliveryTrackingEvents.Add(new DeliveryTrackingEvent { DeliveryId = delivery.Id, AssignmentId = assignment.Id, Source = "MERKATO", EventKey = $"seller-completed:{assignment.Id}", Status = "DELIVERED", Description = "Seller delivery completed after buyer OTP verification." });
            await _db.SaveChangesAsync(ct);
            return Ok(new { completed.DeliveryId, status = completed.Status.ToString(), completed.DeliveredAt });
        }
        catch (UnauthorizedAccessException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("deliveries/{deliveryId:long}/assignments")]
    [Authorize(Roles = "MERCHANT,ADMIN")]
    public async Task<IActionResult> Assign(long deliveryId, [FromBody] DeliveryAssignmentRequest request, CancellationToken ct)
    {
        var actorId = CurrentUserId;
        if (actorId is null) return Unauthorized();
        var delivery = await _db.Deliveries.FirstOrDefaultAsync(x => x.Id == deliveryId, ct);
        if (delivery is null) return NotFound();
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == delivery.OrderId, ct);
        if (order is null) return NotFound();
        if (!User.IsInRole("ADMIN") && order.SellerId != actorId)
            return NotFound();

        var providerCode = request.ProviderCode?.Trim().ToUpperInvariant();
        var provider = await _db.DeliveryProviders.FirstOrDefaultAsync(x => x.Code == providerCode && x.IsEnabled, ct);
        if (provider is null) return BadRequest(new { message = "This delivery provider is not enabled." });
        if (request.AgreedFeeEtb < 0 || request.AgreedFeeEtb > 1000000 || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100)
            return BadRequest(new { message = "Provide a valid delivery fee and idempotency key." });

        if (provider.ProviderKind == "COMPANY" && !provider.SupportsBooking)
            return Conflict(new { message = "This carrier is not bookable yet. Official partner API integration is required." });

        if (provider.ProviderKind == "INDIVIDUAL")
        {
            if (request.PartnerUserId is null) return BadRequest(new { message = "Choose an approved individual delivery partner." });
            var profile = await _db.DeliveryPartnerProfiles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == request.PartnerUserId && x.ApprovalStatus == "APPROVED", ct);
            var partner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.PartnerUserId && !x.IsSuspended, ct);
            if (profile is null || partner is null) return BadRequest(new { message = "The selected individual courier is not approved or is suspended." });
            var areas = JsonSerializer.Deserialize<string[]>(profile.ServiceAreasJson) ?? [];
            if (!areas.Contains(order.CityArea, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = "The individual courier does not serve this delivery area." });
        }
        else if (provider.Code == "SELLER" && request.PartnerUserId is not null)
            return BadRequest(new { message = "Seller delivery does not accept a separate courier user." });

        var duplicate = await _db.DeliveryAssignments.FirstOrDefaultAsync(x => x.DeliveryId == delivery.Id && x.IdempotencyKey == request.IdempotencyKey, ct);
        if (duplicate is not null)
            return Ok(new { duplicate.Id, duplicate.Status, duplicate.DeliveryId, duplicate.ProviderId, duplicate.AgreedFeeEtb });

        var assignment = new DeliveryAssignment
        {
            DeliveryId = delivery.Id,
            ProviderId = provider.Id,
            PartnerUserId = request.PartnerUserId,
            AssignedByUserId = actorId.Value,
            Status = provider.ProviderKind == "SELLER" ? "ACCEPTED" : "OFFERED",
            AgreedFeeEtb = request.AgreedFeeEtb,
            IdempotencyKey = request.IdempotencyKey.Trim(),
            AcceptedAt = provider.ProviderKind == "SELLER" ? DateTime.UtcNow : null
        };
        if (provider.ProviderKind == "SELLER" && delivery.Status == DeliveryStatus.CREATED)
        {
            DeliveryStateMachine.ValidateTransition(delivery.Status, DeliveryStatus.ASSIGNED_COURIER);
            delivery.Status = DeliveryStatus.ASSIGNED_COURIER;
            delivery.UpdatedAt = DateTime.UtcNow;
            order.DeliveryStatus = DeliveryStatus.ASSIGNED_COURIER;
            order.UpdatedAt = DateTime.UtcNow;
        }
        _db.DeliveryAssignments.Add(assignment);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new { message = "This delivery already has an active assignment. Refresh its tracking history." }); }
        _db.DeliveryTrackingEvents.Add(new DeliveryTrackingEvent
        {
            DeliveryId = delivery.Id, AssignmentId = assignment.Id,
            Source = "MERKATO", EventKey = $"assignment-created:{request.IdempotencyKey}",
            Status = assignment.Status, Description = $"Delivery assigned to {provider.DisplayName}."
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new { assignment.Id, assignment.Status, assignment.DeliveryId, provider.Code, provider.DisplayName, assignment.PartnerUserId, assignment.AgreedFeeEtb });
    }

    [HttpGet("assignments/mine")]
    [Authorize]
    public async Task<IActionResult> MyAssignments(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var assignments = await (
            from assignment in _db.DeliveryAssignments.AsNoTracking()
            join provider in _db.DeliveryProviders.AsNoTracking() on assignment.ProviderId equals provider.Id
            join delivery in _db.Deliveries.AsNoTracking() on assignment.DeliveryId equals delivery.Id
            where assignment.PartnerUserId == userId
            orderby assignment.AssignedAt descending
            select new { assignment.Id, assignment.DeliveryId, assignment.Status, assignment.AgreedFeeEtb, assignment.AssignedAt, assignment.AcceptedAt, provider.DisplayName, provider.Code, DeliveryStatus = delivery.Status }
        ).ToListAsync(ct);
        return Ok(assignments.Select(x => new { x.Id, x.DeliveryId, x.Status, x.AgreedFeeEtb, x.AssignedAt, x.AcceptedAt, x.DisplayName, x.Code, deliveryStatus = x.DeliveryStatus.ToString() }));
    }

    [HttpPost("assignments/{assignmentId:long}/accept")]
    [Authorize]
    public async Task<IActionResult> Accept(long assignmentId, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var assignment = await _db.DeliveryAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
        if (assignment is null || assignment.PartnerUserId != userId) return NotFound();
        var profile = await _db.DeliveryPartnerProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.ApprovalStatus == "APPROVED", ct);
        var activePartner = await _db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && !x.IsSuspended, ct);
        if (profile is null || !activePartner) return Forbid();
        if (assignment.Status != "OFFERED") return Conflict(new { message = "This delivery offer is no longer available." });
        var delivery = await _db.Deliveries.FirstOrDefaultAsync(x => x.Id == assignment.DeliveryId, ct);
        if (delivery is null) return NotFound();
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == delivery.OrderId, ct);
        if (order is null) return NotFound();
        if (delivery.Status == DeliveryStatus.CREATED)
        {
            DeliveryStateMachine.ValidateTransition(delivery.Status, DeliveryStatus.ASSIGNED_COURIER);
            delivery.Status = DeliveryStatus.ASSIGNED_COURIER;
            delivery.UpdatedAt = DateTime.UtcNow;
            order.DeliveryStatus = DeliveryStatus.ASSIGNED_COURIER;
            order.UpdatedAt = DateTime.UtcNow;
        }
        var courier = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
        assignment.Status = "ACCEPTED";
        assignment.AcceptedAt = DateTime.UtcNow;
        delivery.RiderName = courier?.FullName;
        delivery.RiderPhone = courier?.PhoneNumber;
        _db.DeliveryTrackingEvents.Add(new DeliveryTrackingEvent { DeliveryId = assignment.DeliveryId, AssignmentId = assignment.Id, Source = "MERKATO", EventKey = $"assignment-accepted:{assignment.Id}", Status = "ACCEPTED", Description = "Individual courier accepted the delivery." });
        await _db.SaveChangesAsync(ct);
        return Ok(new { assignment.Id, assignment.Status, assignment.AcceptedAt });
    }

    [HttpPost("assignments/{assignmentId:long}/reject")]
    [Authorize]
    public async Task<IActionResult> Reject(long assignmentId, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var assignment = await _db.DeliveryAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
        if (assignment is null || assignment.PartnerUserId != userId) return NotFound();
        if (assignment.Status != "OFFERED") return Conflict(new { message = "This delivery offer is no longer available." });
        assignment.Status = "REJECTED";
        _db.DeliveryTrackingEvents.Add(new DeliveryTrackingEvent { DeliveryId = assignment.DeliveryId, AssignmentId = assignment.Id, Source = "MERKATO", EventKey = $"assignment-rejected:{assignment.Id}", Status = "REJECTED", Description = "Individual courier declined the delivery." });
        await _db.SaveChangesAsync(ct);
        return Ok(new { assignment.Id, assignment.Status });
    }

    [HttpPost("assignments/{assignmentId:long}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateAssignedDeliveryStatus(long assignmentId, [FromBody] DeliveryAssignmentStatusRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        if (_deliveryService is null) return StatusCode(503);
        var assignment = await _db.DeliveryAssignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignmentId && x.PartnerUserId == userId && x.Status == "ACCEPTED", ct);
        if (assignment is null) return NotFound();
        if (!Enum.TryParse<DeliveryStatus>(request.Status, true, out var nextStatus) ||
            nextStatus is DeliveryStatus.CREATED or DeliveryStatus.ASSIGNED_COURIER or DeliveryStatus.DELIVERED)
            return BadRequest(new { message = "Choose a valid courier progress status. Delivery completion requires buyer OTP." });
        var delivery = await _db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignment.DeliveryId, ct);
        if (delivery is null) return NotFound();
        try
        {
            var updated = await _deliveryService.UpdateDeliveryStatusAsync(delivery.DeliveryId, nextStatus, ct);
            _db.DeliveryTrackingEvents.Add(new DeliveryTrackingEvent { DeliveryId = delivery.Id, AssignmentId = assignment.Id, Source = "MERKATO", EventKey = $"partner-status:{assignment.Id}:{nextStatus}:{updated.RowVersion}", Status = nextStatus.ToString(), Description = $"Individual courier updated delivery status to {nextStatus}." });
            await _db.SaveChangesAsync(ct);
            return Ok(new { updated.DeliveryId, status = updated.Status.ToString(), updated.UpdatedAt });
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("assignments/{assignmentId:long}/verify-otp")]
    [Authorize]
    public async Task<IActionResult> VerifyAssignedDeliveryOtp(long assignmentId, [FromBody] DeliveryAssignmentOtpRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        if (_deliveryService is null) return StatusCode(503);
        var assignment = await _db.DeliveryAssignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignmentId && x.PartnerUserId == userId && x.Status == "ACCEPTED", ct);
        if (assignment is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.OtpCode) || request.OtpCode.Length is < 4 or > 8 || !request.OtpCode.All(char.IsDigit))
            return BadRequest(new { message = "OTP must contain 4–8 digits." });
        var delivery = await _db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignment.DeliveryId, ct);
        if (delivery is null) return NotFound();
        try
        {
            var completed = await _deliveryService.VerifyOtpAndCompleteHandoverAsync(delivery.DeliveryId, userId.Value, request.OtpCode, ct);
            assignment = await _db.DeliveryAssignments.FirstAsync(x => x.Id == assignmentId, ct);
            assignment.Status = "COMPLETED";
            assignment.CompletedAt = DateTime.UtcNow;
            _db.DeliveryTrackingEvents.Add(new DeliveryTrackingEvent { DeliveryId = delivery.Id, AssignmentId = assignment.Id, Source = "MERKATO", EventKey = $"partner-completed:{assignment.Id}", Status = "DELIVERED", Description = "Buyer OTP verified and delivery completed." });
            await _db.SaveChangesAsync(ct);
            return Ok(new { completed.DeliveryId, status = completed.Status.ToString(), completed.DeliveredAt });
        }
        catch (UnauthorizedAccessException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("deliveries/{deliveryId:long}/tracking")]
    [Authorize]
    public async Task<IActionResult> Tracking(long deliveryId, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var delivery = await _db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == deliveryId, ct);
        if (delivery is null) return NotFound();
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == delivery.OrderId, ct);
        if (order is null) return NotFound();
        var authorized = User.IsInRole("ADMIN") || order.BuyerId == userId || order.SellerId == userId ||
            await _db.DeliveryAssignments.AnyAsync(x => x.DeliveryId == deliveryId && x.PartnerUserId == userId, ct);
        if (!authorized) return NotFound();
        var events = await _db.DeliveryTrackingEvents.AsNoTracking().Where(x => x.DeliveryId == deliveryId)
            .OrderBy(x => x.OccurredAt).Select(x => new { x.Status, x.Description, x.Source, x.OccurredAt }).ToListAsync(ct);
        return Ok(new { delivery.Id, delivery.DeliveryId, delivery.Status, delivery.FeeEtb, events });
    }
}

public sealed record DeliveryPartnerApplication(string[]? ServiceAreas, string TransportMode, string? VehicleDescription, decimal? CapacityKg);
public sealed record DeliveryPartnerReview(string Status, string? Note);
public sealed record DeliveryAssignmentRequest(string ProviderCode, long? PartnerUserId, decimal AgreedFeeEtb, string IdempotencyKey);

public sealed record DeliveryAssignmentStatusRequest(string Status);
public sealed record DeliveryAssignmentOtpRequest(string OtpCode);
