using System.Security.Claims;
using System.Text;
using Merkato.Api.Services;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController(MerkatoDbContext db) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.Id == UserId)
            .Select(x => new { x.Id, x.FullName, x.PhoneNumber, x.Email, x.Role, x.TrustScore, x.IsFaydaVerified, x.CreatedAt, hasProfilePhoto = x.ProfilePhotoContent != null })
            .SingleOrDefaultAsync(ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateAccountProfileRequest request, CancellationToken ct)
    {
        var name = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120) return BadRequest(new { message = "Full name is required and must be at most 120 characters." });
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == UserId, ct);
        if (user is null) return NotFound();
        user.FullName = name; user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.FullName, user.PhoneNumber, user.Email });
    }

    [HttpPut("profile/photo")][Consumes("multipart/form-data")][RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto([FromForm] IFormFile? photo, CancellationToken ct)
    {
        if (photo is null || photo.Length <= 0 || photo.Length > 3 * 1024 * 1024) return BadRequest(new { message = "Choose a profile image up to 3 MB." });
        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant(); var type = photo.ContentType.ToLowerInvariant();
        if (!((ext is ".jpg" or ".jpeg") && type == "image/jpeg") && !(ext == ".png" && type == "image/png") && !(ext == ".webp" && type == "image/webp")) return BadRequest(new { message = "Only JPEG, PNG, and WebP images are accepted." });
        await using var stream = new MemoryStream(); await photo.CopyToAsync(stream, ct); var bytes = stream.ToArray();
        var valid = ext switch {
            ".png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255,
            ".webp" => bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
            _ => false
        };
        if (!valid) return BadRequest(new { message = "The uploaded file is not a valid image." });
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == UserId, ct); if (user is null) return NotFound();
        user.ProfilePhotoContent = bytes; user.ProfilePhotoContentType = type; user.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        return Ok(new { message = "Profile photo updated.", hasProfilePhoto = true });
    }

    [HttpGet("profile/photo")]
    public async Task<IActionResult> GetPhoto(CancellationToken ct)
    {
        var photo = await db.Users.AsNoTracking().Where(x => x.Id == UserId).Select(x => new { x.ProfilePhotoContent, x.ProfilePhotoContentType }).SingleOrDefaultAsync(ct);
        if (photo?.ProfilePhotoContent is null || string.IsNullOrWhiteSpace(photo.ProfilePhotoContentType)) return NotFound();
        Response.Headers.CacheControl = "private, no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(photo.ProfilePhotoContent, photo.ProfilePhotoContentType);
    }

    [HttpDelete("profile/photo")]
    public async Task<IActionResult> DeletePhoto(CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == UserId, ct); if (user is null) return NotFound();
        user.ProfilePhotoContent = null; user.ProfilePhotoContentType = null; user.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpGet("addresses")]
    public async Task<IActionResult> Addresses(CancellationToken ct) => Ok(await db.UserAddresses.AsNoTracking().Where(x => x.UserId == UserId).OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.UpdatedAt).ToListAsync(ct));

    [HttpPost("addresses")]
    public async Task<IActionResult> CreateAddress(UserAddressInput request, CancellationToken ct)
    {
        if (!Valid(request)) return BadRequest(new { message = "Provide recipient, valid Ethiopian phone, city, and sub-city." });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var isDefault = request.IsDefault || !await db.UserAddresses.AnyAsync(x => x.UserId == UserId, ct);
        if (isDefault) await db.UserAddresses.Where(x => x.UserId == UserId && x.IsDefault).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), ct);
        var row = Map(request); row.UserId = UserId; row.IsDefault = isDefault; db.UserAddresses.Add(row);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return CreatedAtAction(nameof(Addresses), new { }, row);
    }

    [HttpPut("addresses/{id:long}")]
    public async Task<IActionResult> UpdateAddress(long id, UserAddressInput request, CancellationToken ct)
    {
        if (!Valid(request)) return BadRequest(new { message = "Provide recipient, valid Ethiopian phone, city, and sub-city." });
        var row = await db.UserAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct); if (row is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        row.Label = Clean(request.Label, 40, "Home"); row.RecipientName = request.RecipientName.Trim(); row.PhoneNumber = Normalize(request.PhoneNumber);
        row.City = request.City.Trim(); row.SubCity = request.SubCity.Trim(); row.Woreda = Optional(request.Woreda); row.StreetAddress = Optional(request.StreetAddress); row.DeliveryInstructions = Optional(request.DeliveryInstructions); row.UpdatedAt = DateTime.UtcNow;
        if (request.IsDefault) { await db.UserAddresses.Where(x => x.UserId == UserId && x.Id != id && x.IsDefault).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), ct); row.IsDefault = true; }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(row);
    }

    [HttpPost("addresses/{id:long}/default")]
    public async Task<IActionResult> SetDefault(long id, CancellationToken ct)
    {
        if (!await db.UserAddresses.AnyAsync(x => x.Id == id && x.UserId == UserId, ct)) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.UserAddresses.Where(x => x.UserId == UserId && x.IsDefault).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), ct);
        await db.UserAddresses.Where(x => x.Id == id && x.UserId == UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, true).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        await tx.CommitAsync(ct); return NoContent();
    }

    [HttpDelete("addresses/{id:long}")]
    public async Task<IActionResult> DeleteAddress(long id, CancellationToken ct)
    {
        var row = await db.UserAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct); if (row is null) return NoContent();
        var wasDefault = row.IsDefault; db.UserAddresses.Remove(row); await db.SaveChangesAsync(ct);
        if (wasDefault) { var next = await db.UserAddresses.Where(x => x.UserId == UserId).OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(ct); if (next is not null) { next.IsDefault = true; next.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); } }
        return NoContent();
    }

    private static bool Valid(UserAddressInput x) => !string.IsNullOrWhiteSpace(x.RecipientName) && x.RecipientName.Trim().Length <= 120 && !string.IsNullOrWhiteSpace(x.City) && x.City.Trim().Length <= 100 && !string.IsNullOrWhiteSpace(x.SubCity) && x.SubCity.Trim().Length <= 100 && EthiopianPhoneNumber.TryNormalize(x.PhoneNumber, out _);
    private static string Normalize(string x) => EthiopianPhoneNumber.TryNormalize(x, out var value) ? value : x.Trim();
    private static string Clean(string? x, int max, string fallback) => string.IsNullOrWhiteSpace(x) ? fallback : x.Trim()[..Math.Min(x.Trim().Length, max)];
    private static string? Optional(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim()[..Math.Min(x.Trim().Length, 500)];
    private static UserAddress Map(UserAddressInput x) => new() { Label = Clean(x.Label, 40, "Home"), RecipientName = x.RecipientName.Trim(), PhoneNumber = Normalize(x.PhoneNumber), City = x.City.Trim(), SubCity = x.SubCity.Trim(), Woreda = Optional(x.Woreda), StreetAddress = Optional(x.StreetAddress), DeliveryInstructions = Optional(x.DeliveryInstructions) };
}

public sealed record UpdateAccountProfileRequest(string FullName);
public sealed record UserAddressInput(string Label, string RecipientName, string PhoneNumber, string City, string SubCity, string? Woreda, string? StreetAddress, string? DeliveryInstructions, bool IsDefault);