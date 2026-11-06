using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/v1/affiliates")]
[Authorize]
public sealed class AffiliateProfilesController(MerkatoDbContext db) : ControllerBase
{
    private long? CurrentUserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var profile = await db.AffiliateProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (profile is null) return Ok(new { enrolled = false, referralCode = (string?)null, isActive = false, referralUrl = (string?)null });
        return Ok(new { enrolled = true, profile.ReferralCode, profile.IsActive, referralUrl = "/register?ref=" + Uri.EscapeDataString(profile.ReferralCode) });
    }

    [HttpPost("enroll")]
    [Authorize(Roles = "BUYER,MERCHANT")]
    public async Task<IActionResult> Enroll(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var existing = await db.AffiliateProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (existing is not null) return Ok(new { existing.Id, existing.ReferralCode, existing.IsActive });

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var code = "MK" + Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
            var profile = new AffiliateProfile { UserId = userId.Value, ReferralCode = code, IsActive = true, CreatedAt = DateTime.UtcNow };
            db.AffiliateProfiles.Add(profile);
            try
            {
                await db.SaveChangesAsync(ct);
                return CreatedAtAction(nameof(Profile), new { }, new { profile.Id, profile.ReferralCode, profile.IsActive });
            }
            catch (DbUpdateException)
            {
                db.Entry(profile).State = EntityState.Detached;
                existing = await db.AffiliateProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
                if (existing is not null) return Ok(new { existing.Id, existing.ReferralCode, existing.IsActive });
            }
        }
        return Conflict(new { error = "Could not allocate a unique referral code. Please retry." });
    }

    [HttpGet("referrals/mine")]
    [Authorize(Roles = "BUYER,MERCHANT")]
    public async Task<IActionResult> Referrals(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var rows = await db.AffiliateReferrals.AsNoTracking()
            .Where(x => x.AffiliateUserId == userId)
            .OrderByDescending(x => x.RegisteredAt ?? x.ClickedAt)
            .Select(x => new { x.Id, x.ReferredUserId, x.ClickedAt, x.RegisteredAt, converted = x.ReferredUserId != null })
            .Take(200).ToListAsync(ct);
        return Ok(new { count = rows.Count, registeredCount = rows.Count(x => x.converted), items = rows });
    }
}
