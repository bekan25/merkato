using System.Security.Claims;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/commerce")]
public sealed class CommerceGrowthController(MerkatoDbContext db) : ControllerBase
{
    private long CurrentUserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [AllowAnonymous]
    [HttpGet("products/{productId:long}/reviews")]
    public async Task<IActionResult> Reviews(long productId, CancellationToken ct)
    {
        var rows = await db.ProductReviews.AsNoTracking().Where(x => x.ProductId == productId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.BuyerId, x.Rating, x.Comment, x.CreatedAt, verifiedPurchase = true })
            .ToListAsync(ct);
        return Ok(new { count = rows.Count, averageRating = rows.Count == 0 ? 0 : Math.Round(rows.Average(x => x.Rating), 2), reviews = rows });
    }

    [Authorize(Roles = "BUYER")]
    [HttpPost("products/{productId:long}/reviews")]
    public async Task<IActionResult> AddReview(long productId, ReviewInput input, CancellationToken ct)
    {
        if (input.Rating is < 1 or > 5 || input.Comment?.Length > 2000) return BadRequest("Rating must be 1-5 and comment max 2000 characters.");
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId && x.IsActive, ct);
        if (product is null) return NotFound();
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.OrderId && x.BuyerId == CurrentUserId && x.SellerId == product.SellerId && x.DeliveryStatus == DeliveryStatus.DELIVERED && x.ProductTitle == product.Name, ct);
        if (order is null) return Conflict("A delivered purchase of this product is required before reviewing.");
        var review = new ProductReview { ProductId = productId, BuyerId = CurrentUserId, OrderId = order.Id, Rating = input.Rating, Comment = (input.Comment ?? "").Trim() };
        db.ProductReviews.Add(review);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict("You have already reviewed this product."); }
        return CreatedAtAction(nameof(Reviews), new { productId }, new { review.Id, review.Rating, review.Comment, verifiedPurchase = true });
    }

    [AllowAnonymous]
    [HttpGet("products/{productId:long}/price-history")]
    public async Task<IActionResult> PriceHistory(long productId, CancellationToken ct)
    {
        var rows = await db.ProductPriceSnapshots.AsNoTracking().Where(x => x.ProductId == productId)
            .OrderBy(x => x.RecordedAt).Select(x => new { x.PriceEtb, x.RecordedAt }).Take(365).ToListAsync(ct);
        return Ok(rows);
    }

    [AllowAnonymous]
    [HttpGet("products/{productId:long}/seller-prices")]
    public async Task<IActionResult> SellerPrices(long productId, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId && x.IsActive, ct);
        if (product is null) return NotFound();
        var rows = await db.Products.AsNoTracking().Where(x => x.IsActive && x.Name.ToLower() == product.Name.ToLower())
            .OrderBy(x => x.PriceEtb).Select(x => new { x.Id, x.SellerId, x.Name, x.PriceEtb, x.Location, x.ImageUrl }).ToListAsync(ct);
        return Ok(rows);
    }

    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpPut("products/{productId:long}/stock")]
    public async Task<IActionResult> UpdateStock(long productId, StockInput input, CancellationToken ct)
    {
        if (input.Quantity < 0 || input.LowStockThreshold < 0) return BadRequest("Stock and threshold cannot be negative.");
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == productId, ct);
        if (product is null) return NotFound();
        if (!User.IsInRole("ADMIN") && product.SellerId != CurrentUserId) return Forbid();
        var detail = await db.ProductListingDetails.FirstOrDefaultAsync(x => x.ProductId == productId, ct);
        if (detail is null) { detail = new ProductListingDetail { ProductId = productId }; db.ProductListingDetails.Add(detail); }
        detail.StockQuantity = input.Quantity;
        detail.LowStockThreshold = input.LowStockThreshold;
        if (Enum.TryParse<ProductCondition>(input.Condition, true, out var condition)) detail.Condition = condition;
        else return BadRequest("Condition must be NEW, USED or REFURBISHED.");
        detail.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { detail.ProductId, detail.StockQuantity, detail.LowStockThreshold, condition = detail.Condition.ToString(), lowStock = detail.StockQuantity <= detail.LowStockThreshold });
    }

    [AllowAnonymous]
    [HttpGet("products/{productId:long}/condition-stock")]
    public async Task<IActionResult> ConditionStock(long productId, CancellationToken ct)
    {
        var row = await db.ProductListingDetails.AsNoTracking().FirstOrDefaultAsync(x => x.ProductId == productId, ct);
        return Ok(row is null ? new { condition = "NEW", stockQuantity = (int?)null, lowStock = false } : new { condition = row.Condition.ToString(), stockQuantity = (int?)row.StockQuantity, lowStock = row.StockQuantity <= row.LowStockThreshold });
    }

    [AllowAnonymous]
    [HttpGet("group-buys")]
    public async Task<IActionResult> GroupBuys(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await db.GroupBuys.AsNoTracking().Where(x => x.Status == GroupBuyStatus.OPEN && x.ExpiresAt > now)
            .OrderBy(x => x.ExpiresAt).Select(x => new { x.Id, x.ProductId, productName = db.Products.Where(p => p.Id == x.ProductId).Select(p => p.Name).FirstOrDefault(), x.OrganizerId, x.TargetQuantity, x.JoinedQuantity, x.RegularUnitPriceEtb, x.DiscountUnitPriceEtb, x.ExpiresAt, progressPercent = Math.Min(100, (int)(100m * x.JoinedQuantity / x.TargetQuantity)) }).ToListAsync(ct);
        return Ok(rows);
    }

    [Authorize(Roles = "BUYER,MERCHANT")]
    [HttpPost("group-buys")]
    public async Task<IActionResult> CreateGroupBuy(GroupBuyInput input, CancellationToken ct)
    {
        if (input.TargetQuantity < 2 || input.DiscountUnitPriceEtb <= 0 || input.RegularUnitPriceEtb <= input.DiscountUnitPriceEtb || input.ExpiresAt <= DateTime.UtcNow || input.ExpiresAt > DateTime.UtcNow.AddDays(30)) return BadRequest("Invalid group target, discount or expiry (maximum 30 days).");
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.ProductId && x.IsActive, ct);
        if (product is null) return NotFound("Product not found.");
        var group = new GroupBuy { ProductId = product.Id, OrganizerId = CurrentUserId, TargetQuantity = input.TargetQuantity, JoinedQuantity = 1, RegularUnitPriceEtb = input.RegularUnitPriceEtb, DiscountUnitPriceEtb = input.DiscountUnitPriceEtb, ExpiresAt = input.ExpiresAt };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.GroupBuys.Add(group);
        await db.SaveChangesAsync(ct);
        db.GroupBuyMembers.Add(new GroupBuyMember { GroupBuyId = group.Id, BuyerId = CurrentUserId, Quantity = 1 });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return CreatedAtAction(nameof(GroupBuys), new { id = group.Id }, new { group.Id, group.Status, group.TargetQuantity });
    }

    [Authorize(Roles = "BUYER")]
    [HttpPost("group-buys/{id:long}/join")]
    public async Task<IActionResult> JoinGroupBuy(long id, GroupJoinInput input, CancellationToken ct)
    {
        if (input.Quantity < 1 || input.Quantity > 1000) return BadRequest("Quantity must be between 1 and 1000.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var group = await db.GroupBuys.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (group is null) return NotFound();
        if (group.Status != GroupBuyStatus.OPEN || group.ExpiresAt <= DateTime.UtcNow) return Conflict("This group buy is closed.");
        if (group.OrganizerId == CurrentUserId) return BadRequest("Organizer cannot join their own group.");
        var stock = await db.ProductListingDetails.FirstOrDefaultAsync(x => x.ProductId == group.ProductId, ct);
        if (stock is not null && stock.StockQuantity < group.JoinedQuantity + input.Quantity) return Conflict("Not enough stock for this group quantity.");
        if (group.JoinedQuantity + input.Quantity > group.TargetQuantity) return Conflict("This quantity exceeds the remaining group target.");
        var member = new GroupBuyMember { GroupBuyId = id, BuyerId = CurrentUserId, Quantity = input.Quantity };
        db.GroupBuyMembers.Add(member);
        group.JoinedQuantity += input.Quantity;
        if (group.JoinedQuantity >= group.TargetQuantity) group.Status = GroupBuyStatus.TARGET_REACHED;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { group.Id, group.JoinedQuantity, group.TargetQuantity, status = group.Status.ToString(), group.DiscountUnitPriceEtb, totalEtb = group.DiscountUnitPriceEtb * input.Quantity });
    }

    [Authorize(Roles = "BUYER")]
    [HttpPost("orders/{orderId:long}/returns")]
    public async Task<IActionResult> RequestReturn(long orderId, ReturnInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 2000) return BadRequest("A return reason is required (max 2000 characters).");
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == orderId && x.BuyerId == CurrentUserId, ct);
        if (order is null) return NotFound();
        var delivery = await db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
        var deliveredAt = delivery?.DeliveredAt ?? order.CreatedAt;
        if (order.DeliveryStatus != DeliveryStatus.DELIVERED || deliveredAt < DateTime.UtcNow.AddDays(-7)) return Conflict("Returns are available within 7 days of delivery only.");
        if (order.EscrowState == EscrowState.RELEASED) return Conflict("The inspection/escrow has already been released; contact support to open a dispute.");
        if (await db.ReturnRequests.AnyAsync(x => x.OrderId == orderId, ct)) return Conflict("A return request already exists for this order.");
        var request = new ReturnRequest { OrderId = order.Id, BuyerId = order.BuyerId, SellerId = order.SellerId, Reason = input.Reason.Trim(), EvidenceUrl = input.EvidenceUrl };
        db.ReturnRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return Ok(new { request.Id, status = request.Status.ToString(), request.CreatedAt });
    }

    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpGet("returns/inbox")]
    public async Task<IActionResult> ReturnInbox(CancellationToken ct)
    {
        var q = db.ReturnRequests.AsNoTracking();
        if (!User.IsInRole("ADMIN")) q = q.Where(x => x.SellerId == CurrentUserId);
        return Ok(await q.OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.OrderId, x.BuyerId, x.SellerId, x.Reason, x.EvidenceUrl, status = x.Status.ToString(), x.ResolutionNote, x.CreatedAt }).ToListAsync(ct));
    }

    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpPost("returns/{id:long}/decision")]
    public async Task<IActionResult> DecideReturn(long id, ReturnDecisionInput input, CancellationToken ct)
    {
        var request = await db.ReturnRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return NotFound();
        if (!User.IsInRole("ADMIN") && request.SellerId != CurrentUserId) return Forbid();
        if (request.Status != ReturnStatus.REQUESTED) return Conflict("Return request is no longer awaiting a decision.");
        if (input.Decision is not ("APPROVED" or "REJECTED")) return BadRequest("Decision must be APPROVED or REJECTED.");
        request.Status = input.Decision == "APPROVED" ? ReturnStatus.APPROVED : ReturnStatus.REJECTED;
        request.ResolutionNote = input.Note?.Trim();
        request.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { request.Id, status = request.Status.ToString() });
    }

    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpPost("returns/{id:long}/received")]
    public async Task<IActionResult> MarkReturnReceived(long id, CancellationToken ct)
    {
        var request = await db.ReturnRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return NotFound();
        if (!User.IsInRole("ADMIN") && request.SellerId != CurrentUserId) return Forbid();
        if (request.Status != ReturnStatus.APPROVED) return Conflict("Only an approved return can be marked received.");
        request.Status = ReturnStatus.RECEIVED; request.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { request.Id, status = request.Status.ToString(), nextStep = "Process refund through the verified payment provider, then confirm the provider refund reference with support." });
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("returns/{id:long}/refund-confirmed")]
    public async Task<IActionResult> ConfirmRefund(long id, RefundConfirmationInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.ProviderReference)) return BadRequest("Verified provider refund reference is required.");
        var request = await db.ReturnRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return NotFound();
        if (request.Status != ReturnStatus.RECEIVED) return Conflict("Returned item must be received before refund confirmation.");
        request.Status = ReturnStatus.REFUNDED; request.ResolutionNote = ("Refund reference: " + input.ProviderReference.Trim()).Substring(0, Math.Min(2000, 18 + input.ProviderReference.Trim().Length)); request.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { request.Id, status = request.Status.ToString() });
    }

    [Authorize]
    [HttpGet("loyalty/me")]
    public async Task<IActionResult> Loyalty(CancellationToken ct)
    {
        var account = await db.LoyaltyAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == CurrentUserId, ct);
        var history = await db.LoyaltyTransactions.AsNoTracking().Where(x => x.UserId == CurrentUserId).OrderByDescending(x => x.CreatedAt).Take(50).Select(x => new { x.Points, x.Reason, x.OrderId, x.CreatedAt }).ToListAsync(ct);
        return Ok(new { pointsBalance = account?.PointsBalance ?? 0, history, redemptionRule = "100 points = 10 ETB coupon; coupon redemption is not yet enabled." });
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("loyalty/award")]
    public async Task<IActionResult> AwardLoyalty(LoyaltyAwardInput input, CancellationToken ct)
    {
        if (input.Points is < 1 or > 100000 || string.IsNullOrWhiteSpace(input.Reason)) return BadRequest("Valid points and reason required.");
        if (input.OrderId.HasValue)
        {
            var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.OrderId.Value && x.BuyerId == input.UserId && x.EscrowState == EscrowState.RELEASED, ct);
            if (order is null) return Conflict("Only a released/settled order can earn loyalty points.");
            if (await db.LoyaltyTransactions.AnyAsync(x => x.OrderId == input.OrderId && x.UserId == input.UserId, ct)) return Conflict("Points for this order have already been awarded.");
        }
        var account = await db.LoyaltyAccounts.FirstOrDefaultAsync(x => x.UserId == input.UserId, ct);
        if (account is null) { account = new LoyaltyAccount { UserId = input.UserId }; db.LoyaltyAccounts.Add(account); }
        account.PointsBalance = checked(account.PointsBalance + input.Points); account.UpdatedAt = DateTime.UtcNow;
        db.LoyaltyTransactions.Add(new LoyaltyTransaction { UserId = input.UserId, Points = input.Points, Reason = input.Reason.Trim(), OrderId = input.OrderId });
        await db.SaveChangesAsync(ct);
        return Ok(new { account.UserId, account.PointsBalance });
    }


    [Authorize]
    [HttpGet("orders/{orderId:long}/delivery")]
    public async Task<IActionResult> OrderDelivery(long orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null) return NotFound();
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
        if (role is not ("ADMIN" or "COURIER") && order.BuyerId != CurrentUserId && order.SellerId != CurrentUserId) return Forbid();
        var delivery = await db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (delivery is null) return NotFound("No delivery has been assigned to this order yet.");
        return Ok(new { delivery.DeliveryId, delivery.OrderId, status = delivery.Status.ToString(), delivery.CourierType, delivery.RiderName, delivery.RiderPhone, delivery.FeeEtb, delivery.DispatchedAt, delivery.DeliveredAt, delivery.UpdatedAt });
    }

    [Authorize(Roles = "MERCHANT,ADMIN")]
    [HttpGet("merchant/inventory")]
    public async Task<IActionResult> Inventory(CancellationToken ct)
    {
        var isAdmin = User.IsInRole("ADMIN");
        var userId = CurrentUserId;
        var q = from p in db.Products.AsNoTracking()
                join s in db.ProductListingDetails.AsNoTracking() on p.Id equals s.ProductId into details
                from d in details.DefaultIfEmpty()
                where p.IsActive && (isAdmin || p.SellerId == userId)
                select new { p.Id, p.Name, p.SellerId, p.PriceEtb, stockQuantity = d == null ? 0 : d.StockQuantity, lowStockThreshold = d == null ? 5 : d.LowStockThreshold, condition = d == null ? "NEW" : d.Condition.ToString(), lowStock = d == null || d.StockQuantity <= d.LowStockThreshold };
        return Ok(await q.OrderBy(x => x.stockQuantity).ToListAsync(ct));
    }

    [Authorize]
    [HttpGet("referrals/me")]
    public async Task<IActionResult> MyReferral(CancellationToken ct)
    {
        var profile = await db.AffiliateProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == CurrentUserId && x.IsActive, ct);
        if (profile is null) return Ok(new { referralCode = (string?)null, referrals = 0, message = "Create a referral profile to start sharing." });
        var referrals = await db.AffiliateReferrals.AsNoTracking().CountAsync(x => x.AffiliateUserId == CurrentUserId && x.ReferredUserId != null, ct);
        var commissions = await db.AffiliateCommissions.AsNoTracking().Where(x => x.AffiliateUserId == CurrentUserId)
            .Select(x => new { x.CommissionEtb, x.Status, x.CreatedAt }).ToListAsync(ct);
        return Ok(new { profile.ReferralCode, referrals, commissions, totalCommissionEtb = commissions.Sum(x => x.CommissionEtb) });
    }

    [Authorize]
    [HttpPost("referrals/me")]
    public async Task<IActionResult> CreateReferralProfile(CancellationToken ct)
    {
        var existing = await db.AffiliateProfiles.FirstOrDefaultAsync(x => x.UserId == CurrentUserId, ct);
        if (existing is not null) return Ok(new { existing.ReferralCode, existing.IsActive });
        var code = "MK" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6));
        var profile = new AffiliateProfile { UserId = CurrentUserId, ReferralCode = code };
        db.AffiliateProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(MyReferral), new { }, new { profile.ReferralCode, profile.IsActive });
    }

    [AllowAnonymous]
    [HttpPost("referrals/{code}/click")]
    public async Task<IActionResult> TrackReferralClick(string code, ReferralClickInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.VisitorKey) || input.VisitorKey.Length > 200) return BadRequest("A visitor key is required.");
        var profile = await db.AffiliateProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.ReferralCode == code && x.IsActive, ct);
        if (profile is null) return NotFound("Referral code not found.");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input.VisitorKey)));
        if (!await db.AffiliateReferrals.AnyAsync(x => x.AffiliateUserId == profile.UserId && x.VisitorKeyHash == hash, ct))
        {
            db.AffiliateReferrals.Add(new AffiliateReferral { AffiliateUserId = profile.UserId, ReferralCode = code, VisitorKeyHash = hash });
            await db.SaveChangesAsync(ct);
        }
        return Ok(new { tracked = true });
    }

    [Authorize]
    [HttpPost("referrals/{code}/claim")]
    public async Task<IActionResult> ClaimReferral(string code, ReferralClaimInput input, CancellationToken ct)
    {
        var profile = await db.AffiliateProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.ReferralCode == code && x.IsActive, ct);
        if (profile is null) return NotFound("Referral code not found.");
        if (profile.UserId == CurrentUserId) return BadRequest("You cannot claim your own referral code.");
        if (await db.AffiliateReferrals.AnyAsync(x => x.ReferredUserId == CurrentUserId, ct)) return Conflict("This account has already used a referral code.");
        if (string.IsNullOrWhiteSpace(input.VisitorKey) || input.VisitorKey.Length > 200) return BadRequest("Valid visitor key required.");
        var visitorHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input.VisitorKey)));
        var pending = await db.AffiliateReferrals.FirstOrDefaultAsync(x => x.AffiliateUserId == profile.UserId && x.VisitorKeyHash == visitorHash && x.ReferredUserId == null, ct);
        if (pending is null) return Conflict("No tracked referral visit was found for this account.");
        pending.ReferredUserId = CurrentUserId;
        pending.RegisteredAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { claimed = true, referrerUserId = profile.UserId });
    }

}

public sealed record ReviewInput(long OrderId, int Rating, string? Comment);
public sealed record StockInput(int Quantity, int LowStockThreshold, string Condition = "NEW");
public sealed record GroupBuyInput(long ProductId, int TargetQuantity, decimal RegularUnitPriceEtb, decimal DiscountUnitPriceEtb, DateTime ExpiresAt);
public sealed record GroupJoinInput(int Quantity);
public sealed record ReturnInput(string Reason, string? EvidenceUrl);
public sealed record ReturnDecisionInput(string Decision, string? Note);
public sealed record RefundConfirmationInput(string ProviderReference);
public sealed record LoyaltyAwardInput(long UserId, int Points, string Reason, long? OrderId);

public sealed record ReferralClickInput(string VisitorKey);

public sealed record ReferralClaimInput(string VisitorKey);
