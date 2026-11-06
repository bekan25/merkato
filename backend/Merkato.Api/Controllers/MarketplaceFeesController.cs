using System.Security.Claims;
using System.Text.Json;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/v1/marketplace/fees")]
public sealed class MarketplaceFeesController(MerkatoDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var settings = await db.MarketplaceFeeSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, ct);
        settings ??= new MarketplaceFeeSettings();
        return Ok(ToResponse(settings));
    }

    [HttpPut]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Update([FromBody] UpdateMarketplaceFeesRequest input, CancellationToken ct)
    {
        if (input.MerchantCommissionRatePercent is < 0m or > 10m)
            return BadRequest(new { message = "Merchant commission must be between 0% and 10%." });
        if (input.BuyerServiceFeeRatePercent is < 0m or > 5m)
            return BadRequest(new { message = "Buyer service fee must be between 0% and 5%." });

        var settings = await db.MarketplaceFeeSettings.FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (settings is null)
        {
            settings = new MarketplaceFeeSettings { Id = 1 };
            db.MarketplaceFeeSettings.Add(settings);
        }

        settings.MerchantCommissionRatePercent = input.MerchantCommissionRatePercent;
        settings.BuyerServiceFeeRatePercent = input.BuyerServiceFeeRatePercent;
        settings.IsEnabled = input.IsEnabled;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByUserId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

        db.AuditLogs.Add(new AuditLog
        {
            ActorId = settings.UpdatedByUserId,
            ActorRole = "ADMIN",
            Action = "MARKETPLACE_FEES_UPDATED",
            EntityType = "MarketplaceFeeSettings",
            EntityId = "1",
            MetadataJson = JsonSerializer.Serialize(new
            {
                settings.MerchantCommissionRatePercent,
                settings.BuyerServiceFeeRatePercent,
                settings.IsEnabled
            }),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(settings));
    }

    private static object ToResponse(MarketplaceFeeSettings settings) => new
    {
        merchantCommissionRatePercent = settings.MerchantCommissionRatePercent,
        buyerServiceFeeRatePercent = settings.BuyerServiceFeeRatePercent,
        isEnabled = settings.IsEnabled,
        currency = "ETB",
        updatedAt = settings.UpdatedAt
    };
}

public sealed record UpdateMarketplaceFeesRequest(
    decimal MerchantCommissionRatePercent,
    decimal BuyerServiceFeeRatePercent,
    bool IsEnabled = true);
