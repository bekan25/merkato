using Merkato.Api.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using Merkato.Application.DTOs;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MerkatoDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthController(
        MerkatoDbContext db,
        IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    // =========================
    // REGISTER
    // =========================
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            return BadRequest("PhoneNumber is required.");

        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest("FullName is required.");

        if (string.IsNullOrWhiteSpace(dto.Pin) ||
            dto.Pin.Length < 4 ||
            dto.Pin.Length > 8 ||
            dto.Pin.Any(character => !char.IsDigit(character)))
            return BadRequest("PIN must contain 4 to 8 digits.");

        // Public registration must never be able to create privileged accounts.
        if (dto.Role != UserRole.BUYER && dto.Role != UserRole.MERCHANT)
            return BadRequest("Choose either BUYER or MERCHANT.");

        var normalizedPhone = dto.PhoneNumber.Trim();
        var existingUser = await _db.Users
            .AnyAsync(x => x.PhoneNumber == normalizedPhone);

        if (existingUser)
            return Conflict("User with this phone number already exists.");

        var user = new User
        {
            PhoneNumber = normalizedPhone,
            FullName = dto.FullName.Trim(),
            PinHash = PinHasher.Hash(dto.Pin),
            FaydaIdNumber = null,
            Role = dto.Role,
            // A submitted Fayda number is not proof of verification.
            IsFaydaVerified = false,
            TrustScore = 50,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = GenerateToken(user);

        return Ok(new AuthResponseDto(
            token,
            user.Id,
            user.PhoneNumber,
            user.FullName,
            user.Role,
            user.TrustScore,
            user.IsFaydaVerified
        ));
    }

    // =========================
    // LOGIN
    // =========================
    [AllowAnonymous]
[HttpPost("login")]
public async Task<ActionResult<AuthResponseDto>> Login(
    [FromBody] LoginDto dto)
{
    if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
        return BadRequest("PhoneNumber is required.");

    if (string.IsNullOrWhiteSpace(dto.Pin))
        return BadRequest("PIN is required.");

    var user = await _db.Users
        .FirstOrDefaultAsync(x => x.PhoneNumber == dto.PhoneNumber.Trim());

    if (user == null)
        return Unauthorized("Invalid phone number or PIN.");

    if (string.IsNullOrWhiteSpace(user.PinHash) ||
        !PinHasher.Verify(dto.Pin.Trim(), user.PinHash))
    {
        return Unauthorized("Invalid phone number or PIN.");
    }

    var token = GenerateToken(user);

    return Ok(new AuthResponseDto(
        token,
        user.Id,
        user.PhoneNumber,
        user.FullName,
        user.Role,
        user.TrustScore,
        user.IsFaydaVerified
    ));
}

    // =========================
    // CURRENT USER
    // =========================
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync(userId);

        if (user == null)
            return NotFound();

        return Ok(new
        {
            user.Id,
            user.PhoneNumber,
            user.FullName,
            user.Role,
            user.TrustScore,
            user.IsFaydaVerified
        });
    }

    // =========================
    // JWT GENERATOR
    // =========================
    private string GenerateToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(jwtKey))
            throw new InvalidOperationException(
                "Jwt:Key is not configured.");

        var issuer =
            _configuration["Jwt:Issuer"]
            ?? "https://api.merkato.et";

        var audience =
            _configuration["Jwt:Audience"]
            ?? "https://merkato.et";

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new Claim(
                ClaimTypes.Name,
                user.FullName),

            new Claim(
                ClaimTypes.MobilePhone,
                user.PhoneNumber),

            new Claim(
                ClaimTypes.Role,
                user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials
         );

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}


