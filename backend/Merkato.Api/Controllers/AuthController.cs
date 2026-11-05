using Merkato.Api.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Merkato.Application.DTOs;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LoginLockoutDuration = TimeSpan.FromMinutes(15);
    private readonly MerkatoDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthController(MerkatoDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterUserDto dto)
    {
        if (!EthiopianPhoneNumber.TryNormalize(dto.PhoneNumber, out var normalizedPhone))
            return BadRequest("Enter a valid Ethiopian mobile number (09/07 or +2519/+2517).");

        if (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Trim().Length > 120)
            return BadRequest("FullName is required and must not exceed 120 characters.");

        if (string.IsNullOrWhiteSpace(dto.Pin) || dto.Pin.Length is < 6 or > 8 ||
            dto.Pin.Any(character => character is < '0' or > '9'))
            return BadRequest("PIN must contain 6 to 8 digits.");

        if (dto.Role != UserRole.BUYER && dto.Role != UserRole.MERCHANT)
            return BadRequest("Choose either BUYER or MERCHANT.");

        if (await _db.Users.AnyAsync(x => x.PhoneNumber == normalizedPhone))
            return Conflict("User with this phone number already exists.");

        var user = new User
        {
            PhoneNumber = normalizedPhone,
            FullName = dto.FullName.Trim(),
            PinHash = PinHasher.Hash(dto.Pin),
            FaydaIdNumber = null,
            Role = dto.Role,
            IsFaydaVerified = false,
            TrustScore = 50,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("User with this phone number already exists.");
        }

        return Ok(CreateAuthResponse(user));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        if (!EthiopianPhoneNumber.TryNormalize(dto.PhoneNumber, out var normalizedPhone))
            return Unauthorized("Invalid phone number or PIN.");

        if (string.IsNullOrWhiteSpace(dto.Pin) || dto.Pin.Length > 8)
            return Unauthorized("Invalid phone number or PIN.");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == normalizedPhone);
        if (user is null)
            return Unauthorized("Invalid phone number or PIN.");

        if (user.IsSuspended)
            return StatusCode(StatusCodes.Status403Forbidden, "This account is suspended. Contact MERKATO support.");

        var now = DateTime.UtcNow;
        if (user.LoginLockedUntil is DateTime lockedUntil && lockedUntil > now)
        {
            Response.Headers["Retry-After"] = Math.Max(1, (int)Math.Ceiling((lockedUntil - now).TotalSeconds)).ToString();
            return StatusCode(StatusCodes.Status423Locked, "Too many failed attempts. Try again after the lockout expires.");
        }

        if (user.LoginLockedUntil is not null && user.LoginLockedUntil <= now)
        {
            user.LoginLockedUntil = null;
            user.FailedLoginAttempts = 0;
        }

        if (string.IsNullOrWhiteSpace(user.PinHash) || !PinHasher.Verify(dto.Pin, user.PinHash))
        {
            user.FailedLoginAttempts = Math.Min(user.FailedLoginAttempts + 1, MaxFailedAttempts);
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
                user.LoginLockedUntil = now.Add(LoginLockoutDuration);
            user.UpdatedAt = now;
            await _db.SaveChangesAsync();
            return Unauthorized("Invalid phone number or PIN.");
        }

        user.FailedLoginAttempts = 0;
        user.LoginLockedUntil = null;
        user.UpdatedAt = now;
        await _db.SaveChangesAsync();

        return Ok(CreateAuthResponse(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var userId)) return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user is null) return NotFound();
        if (user.IsSuspended) return StatusCode(StatusCodes.Status403Forbidden, "This account is suspended.");

        return Ok(new
        {
            user.Id,
            user.PhoneNumber,
            user.FullName,
            user.Role,
            user.TrustScore,
            user.IsFaydaVerified,
            user.IsSuspended
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var userId)) return Unauthorized();
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return Unauthorized();
        user.TokenVersion++;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private AuthResponseDto CreateAuthResponse(User user) =>
        new(GenerateToken(user), user.Id, user.PhoneNumber, user.FullName,
            user.Role, user.TrustScore, user.IsFaydaVerified);

    private string GenerateToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
            throw new InvalidOperationException("Jwt:Key must be configured with at least 32 UTF-8 bytes.");

        var issuer = _configuration["Jwt:Issuer"] ?? "https://api.merkato.et";
        var audience = _configuration["Jwt:Audience"] ?? "https://merkato.et";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.MobilePhone, user.PhoneNumber),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("token_version", user.TokenVersion.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var configured)
            ? Math.Clamp(configured, 5, 60) : 30;
        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
