using System.Security.Claims;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/seller-payment-accounts")]
public sealed class SellerPaymentAccountsController : ControllerBase
{
    private readonly MerkatoDbContext _db;

    public SellerPaymentAccountsController(MerkatoDbContext db) => _db = db;

    [HttpGet("mine")]
    [Authorize(Roles = "MERCHANT,ADMIN")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var query = _db.SellerPaymentAccounts.AsNoTracking();
        if (!User.IsInRole("ADMIN")) query = query.Where(x => x.SellerId == userId);
        var accounts = await query.OrderBy(x => x.PaymentRail).Select(x => new
        {
            x.Id, x.SellerId, paymentRail = x.PaymentRail.ToString(),
            x.AccountHolderName, x.AccountIdentifier, x.BankName,
            x.Instructions, x.IsActive, x.CreatedAt, x.UpdatedAt
        }).ToListAsync(ct);
        return Ok(accounts);
    }

    [HttpPut("mine")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> SaveMine([FromBody] SaveSellerPaymentAccountRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var sellerId)) return Unauthorized();
        if (!Enum.IsDefined(request.PaymentRail) || request.PaymentRail == PaymentRail.SANDBOX)
            return BadRequest(new { error = "Choose a supported real payment rail." });

        var holder = request.AccountHolderName?.Trim();
        var identifier = request.AccountIdentifier?.Trim();
        var bank = request.BankName?.Trim();
        var instructions = request.Instructions?.Trim();
        if (string.IsNullOrWhiteSpace(holder) || holder.Length is < 2 or > 120 ||
            string.IsNullOrWhiteSpace(identifier) || identifier.Length is < 4 or > 120 ||
            bank?.Length > 120 || instructions?.Length > 500)
            return BadRequest(new { error = "Provide a valid account holder, account/phone identifier, and optional bank details." });

        if (request.PaymentRail == PaymentRail.BANK_OF_ABYSSINIA && string.IsNullOrWhiteSpace(bank))
            bank = "Bank of Abyssinia";

        var seller = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sellerId && x.Role == UserRole.MERCHANT && !x.IsSuspended, ct);
        if (seller is null) return Forbid();

        var account = await _db.SellerPaymentAccounts
            .FirstOrDefaultAsync(x => x.SellerId == sellerId && x.PaymentRail == request.PaymentRail, ct);
        var now = DateTime.UtcNow;
        if (account is null)
        {
            account = new SellerPaymentAccount { SellerId = sellerId, PaymentRail = request.PaymentRail, CreatedAt = now };
            _db.SellerPaymentAccounts.Add(account);
        }
        account.AccountHolderName = holder;
        account.AccountIdentifier = identifier;
        account.BankName = string.IsNullOrWhiteSpace(bank) ? null : bank;
        account.Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions;
        account.IsActive = request.IsActive;
        account.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            account.Id, account.SellerId, paymentRail = account.PaymentRail.ToString(),
            account.AccountHolderName, account.AccountIdentifier, account.BankName,
            account.Instructions, account.IsActive, account.CreatedAt, account.UpdatedAt
        });
    }

    [HttpDelete("mine/{id:long}")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> DeactivateMine(long id, CancellationToken ct)
    {
        if (!TryGetUserId(out var sellerId)) return Unauthorized();
        var account = await _db.SellerPaymentAccounts
            .FirstOrDefaultAsync(x => x.Id == id && x.SellerId == sellerId, ct);
        if (account is null) return NotFound();
        account.IsActive = false;
        account.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("order/{orderId:long}")]
    [Authorize(Roles = "BUYER,ADMIN")]
    public async Task<IActionResult> ForOrder(long orderId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null) return NotFound(new { error = "Order not found." });
        if (!User.IsInRole("ADMIN") && order.BuyerId != userId) return Forbid();

        var account = await _db.SellerPaymentAccounts.AsNoTracking()
            .Where(x => x.SellerId == order.SellerId && x.PaymentRail == order.PaymentRail && x.IsActive)
            .Select(x => new
            {
                paymentRail = x.PaymentRail.ToString(),
                x.AccountHolderName, x.AccountIdentifier, x.BankName, x.Instructions
            }).FirstOrDefaultAsync(ct);
        if (account is null)
            return Conflict(new { error = "This seller has not configured an active account for the selected payment method. Contact the seller or choose another payment method." });

        return Ok(new
        {
            orderId = order.Id,
            order.OrderNumber,
            order.SellerId,
            amountEtb = order.TotalAmountEtb,
            currency = "ETB",
            paymentDestination = account,
            notice = "Transfer directly to the seller. MERKATO does not receive or hold this payment. Upload your receipt, then wait for the seller to confirm receipt in their own account."
        });
    }

    private bool TryGetUserId(out long userId) =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

public sealed record SaveSellerPaymentAccountRequest(
    PaymentRail PaymentRail,
    string AccountHolderName,
    string AccountIdentifier,
    string? BankName,
    string? Instructions,
    bool IsActive = true);
