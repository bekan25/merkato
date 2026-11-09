using System.Security.Claims;
using Merkato.Application.Interfaces;
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
    private readonly IAuditLogService _audit;

    public SellerPaymentAccountsController(MerkatoDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

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
        var lastFour = identifier.Length <= 4 ? identifier : identifier[^4..];
        await _audit.LogAsync(sellerId, "MERCHANT", "SELLER_PAYMENT_ACCOUNT_SAVED",
            "SELLER_PAYMENT_ACCOUNT", account.Id.ToString(), "SUCCESS",
            $"Payment rail {account.PaymentRail}; account ending {lastFour}; active={account.IsActive}", ct: ct);

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
        await _audit.LogAsync(sellerId, "MERCHANT", "SELLER_PAYMENT_ACCOUNT_DEACTIVATED",
            "SELLER_PAYMENT_ACCOUNT", account.Id.ToString(), "SUCCESS",
            $"Payment rail {account.PaymentRail}", ct: ct);
        return NoContent();
    }

    [HttpGet("chapa/mine")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> ChapaMine(CancellationToken ct)
    {
        if (!TryGetUserId(out var sellerId)) return Unauthorized();
        var account = await _db.SellerChapaAccounts.AsNoTracking()
            .Where(x => x.SellerId == sellerId)
            .Select(x => new
            {
                x.OnboardingStatus,
                x.BusinessName,
                x.AccountName,
                x.BankCode,
                x.AccountNumberLast4,
                x.ProviderReference,
                x.SubmittedAt,
                x.VerifiedAt,
                hasApprovedSubaccount = x.OnboardingStatus == "APPROVED" &&
                    x.ChapaSubaccountId != null
            })
            .FirstOrDefaultAsync(ct);

        return Ok(account ?? new
        {
            OnboardingStatus = "NOT_STARTED",
            BusinessName = (string?)null,
            AccountName = (string?)null,
            BankCode = (string?)null,
            AccountNumberLast4 = (string?)null,
            ProviderReference = (string?)null,
            SubmittedAt = (DateTime?)null,
            VerifiedAt = (DateTime?)null,
            hasApprovedSubaccount = false
        });
    }

    [HttpGet("order/{orderId:long}")]
    [Authorize(Roles = "BUYER,ADMIN")]
    public async Task<IActionResult> ForOrder(long orderId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null) return NotFound(new { error = "Order not found." });
        if (!User.IsInRole("ADMIN") && order.BuyerId != userId) return Forbid();
        if (order.InventoryReservationState is InventoryReservationState.RELEASED or InventoryReservationState.EXPIRED ||
            (order.InventoryReservationState == InventoryReservationState.RESERVED &&
             order.InventoryReservationExpiresAt <= DateTime.UtcNow))
            return Conflict(new { error = "This order's inventory reservation has expired. Place a new order before transferring money." });

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
