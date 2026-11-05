using System.Security.Claims;
using System.Text.Json;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/trust")]
[Authorize]
public sealed class TrustController : ControllerBase
{
    private const string VerificationType = "SELLER_VERIFICATION";
    private readonly MerkatoDbContext _db;
    public TrustController(MerkatoDbContext db) => _db = db;

    [HttpGet("seller-verification/me")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> MyVerification(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var latest = await LatestRequest(userId, ct);
        return Ok(latest == null
            ? new { status = "NOT_SUBMITTED", submittedAt = (DateTime?)null, businessName = (string?)null, businessType = (string?)null, city = (string?)null, contactPhone = (string?)null, reviewerNote = (string?)null }
            : ToResponse(latest));
    }

    [HttpPost("seller-verification")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> SubmitVerification([FromBody] SellerVerificationRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (request == null || string.IsNullOrWhiteSpace(request.BusinessName) ||
            string.IsNullOrWhiteSpace(request.BusinessType) || string.IsNullOrWhiteSpace(request.City) ||
            string.IsNullOrWhiteSpace(request.ContactPhone))
            return BadRequest(new { message = "Business name, business type, city and contact phone are required." });
        if (request.BusinessName.Trim().Length > 160 || request.BusinessType.Trim().Length > 80 ||
            request.City.Trim().Length > 100 || request.ContactPhone.Trim().Length > 30)
            return BadRequest(new { message = "One or more fields exceed the allowed length." });

        var normalizedPhone = new string(request.ContactPhone.Where(ch => !char.IsWhiteSpace(ch) && ch != '-').ToArray());
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedPhone, @"^(\+251|0)?[79][0-9]{8}$"))
            return BadRequest(new { message = "Enter a valid Ethiopian mobile number, e.g. 0912345678 or +251912345678." });

        var latest = await LatestRequest(userId, ct);
        if (latest?.Result == "PENDING") return Conflict(new { message = "Your seller verification request is already pending review." });
        if (latest?.Result == "APPROVED") return Conflict(new { message = "Your seller profile is already approved." });

        var log = new AuditLog
        {
            ActorId = userId, ActorRole = "MERCHANT", Action = "SELLER_VERIFICATION_SUBMITTED",
            EntityType = VerificationType, EntityId = userId.ToString(), Result = "PENDING",
            MetadataJson = JsonSerializer.Serialize(new {
                businessName = request.BusinessName.Trim(), businessType = request.BusinessType.Trim(),
                city = request.City.Trim(), contactPhone = normalizedPhone
            }),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(), TraceId = HttpContext.TraceIdentifier
        };
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        return Ok(new { status = "PENDING", submittedAt = log.CreatedAt });
    }

    [HttpGet("seller-verifications")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> SellerVerificationQueue(CancellationToken ct)
    {
        var latest = await _db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityType == VerificationType)
            .GroupBy(x => x.EntityId)
            .Select(g => g.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).First())
            .OrderBy(x => x.Result == "PENDING" ? 0 : 1)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return Ok(latest.Select(ToResponse));
    }

    [HttpPost("seller-verifications/{sellerId:long}/decision")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> DecideSellerVerification(long sellerId, [FromBody] SellerVerificationDecision request, CancellationToken ct)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();
        if (request == null || (request.Decision != "APPROVED" && request.Decision != "REJECTED"))
            return BadRequest(new { message = "Decision must be APPROVED or REJECTED." });
        if (request.Decision == "REJECTED" && string.IsNullOrWhiteSpace(request.Note))
            return BadRequest(new { message = "A reason is required when rejecting a seller verification." });

        var seller = await _db.Users.FirstOrDefaultAsync(x => x.Id == sellerId && x.Role.ToString() == "MERCHANT", ct);
        if (seller == null) return NotFound(new { message = "Merchant account not found." });
        var latest = await LatestRequest(sellerId, ct);
        if (latest == null) return NotFound(new { message = "No seller verification request exists." });
        if (latest.Result != "PENDING") return Conflict(new { message = "This request has already been reviewed." });

        using var submittedProfile = JsonDocument.Parse(string.IsNullOrWhiteSpace(latest.MetadataJson) ? "{}" : latest.MetadataJson);
        var profile = submittedProfile.RootElement.Clone();
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = adminId, ActorRole = "ADMIN", Action = "SELLER_VERIFICATION_" + request.Decision,
            EntityType = VerificationType, EntityId = sellerId.ToString(), Result = request.Decision,
            MetadataJson = JsonSerializer.Serialize(new {
                businessName = profile.TryGetProperty("businessName", out var businessName) ? businessName.GetString() : null,
                businessType = profile.TryGetProperty("businessType", out var businessType) ? businessType.GetString() : null,
                city = profile.TryGetProperty("city", out var city) ? city.GetString() : null,
                contactPhone = profile.TryGetProperty("contactPhone", out var contactPhone) ? contactPhone.GetString() : null,
                note = request.Note?.Trim(),
                reviewedRequestId = latest.Id
            }),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(), TraceId = HttpContext.TraceIdentifier
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new { sellerId, status = request.Decision, message = "Seller verification decision recorded." });
    }

    private Task<AuditLog?> LatestRequest(long sellerId, CancellationToken ct) =>
        _db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityType == VerificationType && x.EntityId == sellerId.ToString())
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

    private bool TryGetUserId(out long userId) =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static object ToResponse(AuditLog log)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(log.MetadataJson) ? "{}" : log.MetadataJson);
        var root = document.RootElement;
        string? Read(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return new {
            sellerId = long.TryParse(log.EntityId, out var id) ? id : 0,
            status = log.Result, businessName = Read("businessName"), businessType = Read("businessType"),
            city = Read("city"), contactPhone = Read("contactPhone"), reviewerNote = Read("note"),
            submittedAt = log.CreatedAt
        };
    }
}

public sealed class SellerVerificationRequest
{
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
}
public sealed class SellerVerificationDecision
{
    public string Decision { get; set; } = string.Empty;
    public string? Note { get; set; }
}