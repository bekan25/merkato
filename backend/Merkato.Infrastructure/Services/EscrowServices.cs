using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Domain.StateMachines;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Merkato.Infrastructure.Services;

public class LedgerEngine : ILedgerEngine
{
    private readonly MerkatoDbContext _db;

    public LedgerEngine(MerkatoDbContext db)
    {
        _db = db;
    }

    public async Task RecordTransactionAsync(
        long orderId,
        string transactionType,
        string referenceId,
        List<(LedgerAccountType account, LedgerEntryType type, decimal amount, long? userId)> entries,
        CancellationToken ct = default)
    {
        var totalDebits = entries.Where(e => e.type == LedgerEntryType.DEBIT).Sum(e => e.amount);
        var totalCredits = entries.Where(e => e.type == LedgerEntryType.CREDIT).Sum(e => e.amount);

        if (totalDebits != totalCredits)
        {
            throw new InvalidOperationException($"Double-entry imbalance! Debits: {totalDebits}, Credits: {totalCredits}");
        }

        var tx = new LedgerTransaction
        {
            OrderId = orderId,
            TransactionType = transactionType,
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var entry in entries)
        {
            tx.Entries.Add(new LedgerEntry
            {
                AccountType = entry.account,
                EntryType = entry.type,
                AmountEtb = entry.amount,
                UserId = entry.userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        _db.LedgerTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);
    }
}

public class EscrowService : IEscrowService
{
    private readonly MerkatoDbContext _db;
    private readonly ILedgerEngine _ledger;
    private readonly IAuditLogService _audit;

    public EscrowService(MerkatoDbContext db, ILedgerEngine ledger, IAuditLogService audit)
    {
        _db = db;
        _ledger = ledger;
        _audit = audit;
    }

    public async Task<EscrowAccount> LockFundsAsync(long orderId, decimal amountEtb, CancellationToken ct = default)
    {
        if (amountEtb <= 0m)
        {
            throw new InvalidOperationException("Escrow lock amount must be positive.");
        }

        var order = await _db.Orders.FindAsync(new object[] { orderId }, ct)
            ?? throw new KeyNotFoundException($"Order {orderId} not found");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var escrow = await _db.EscrowAccounts.FirstOrDefaultAsync(e => e.OrderId == orderId, ct);
        // Replayed verified webhooks must not reserve stock or create a second ledger lock.
        if (escrow?.Status == EscrowState.HELD)
        {
            return escrow;
        }

        if (escrow == null)
        {
            // New checkouts already own one reserved unit. Consume that reservation
            // atomically; do not compete with group-buy stock or consume another buyer's unit.
            if (order.InventoryReservationState == InventoryReservationState.RESERVED)
            {
                var now = DateTime.UtcNow;
                var consumedClaim = await _db.Orders
                    .Where(x => x.Id == orderId &&
                                x.InventoryReservationState == InventoryReservationState.RESERVED &&
                                x.InventoryReservationExpiresAt > now)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(x => x.InventoryReservationState, InventoryReservationState.CONSUMED)
                        .SetProperty(x => x.UpdatedAt, now), ct);
                if (consumedClaim != 1)
                    throw new InvalidOperationException("The checkout inventory reservation has expired or is no longer available.");

                var consumedStock = await _db.ProductListingDetails
                    .Where(x => x.ProductId == order.ProductId &&
                                x.StockQuantity >= 1 && x.ReservedQuantity >= 1)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(x => x.StockQuantity, x => x.StockQuantity - 1)
                        .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity - 1)
                        .SetProperty(x => x.UpdatedAt, now), ct);
                if (consumedStock != 1)
                    throw new InvalidOperationException("Reserved inventory is inconsistent. Payment cannot be confirmed safely.");
                order.InventoryReservationState = InventoryReservationState.CONSUMED;
            }
            else if (order.InventoryReservationState == InventoryReservationState.RELEASED ||
                     order.InventoryReservationState == InventoryReservationState.EXPIRED)
            {
                throw new InvalidOperationException("The order's inventory reservation is no longer active.");
            }
            else if (order.ProductId.HasValue)
            {
                // Historical orders created before reservation tracking keep a safe legacy path.
                var now = DateTime.UtcNow;
                var consumed = await _db.ProductListingDetails
                    .Where(x => x.ProductId == order.ProductId.Value &&
                                x.StockQuantity - x.ReservedQuantity >= 1)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(x => x.StockQuantity, x => x.StockQuantity - 1)
                        .SetProperty(x => x.UpdatedAt, now), ct);
                if (consumed == 0)
                    throw new InvalidOperationException("This product is out of available stock.");
            }
            else
            {
                var legacyProduct = await _db.Products.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.SellerId == order.SellerId && p.Name == order.ProductTitle, ct);
                if (legacyProduct is not null)
                {
                    var consumed = await _db.ProductListingDetails
                        .Where(x => x.ProductId == legacyProduct.Id && x.StockQuantity - x.ReservedQuantity >= 1)
                        .ExecuteUpdateAsync(update => update
                            .SetProperty(x => x.StockQuantity, x => x.StockQuantity - 1)
                            .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
                    if (consumed == 0)
                        throw new InvalidOperationException("This product is out of available stock.");
                }
            }

            escrow = new EscrowAccount
            {
                OrderId = orderId,
                BuyerId = order.BuyerId,
                SellerId = order.SellerId,
                HeldAmountEtb = amountEtb,
                Status = EscrowState.HELD,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.EscrowAccounts.Add(escrow);
        }
        else
        {
            if (escrow.Status == EscrowState.RELEASED || escrow.Status == EscrowState.REFUNDED)
            {
                throw new InvalidOperationException($"Cannot lock funds after escrow has already reached {escrow.Status}.");
            }

            EscrowStateMachine.ValidateTransition(escrow.Status, EscrowState.HELD);
            escrow.HeldAmountEtb = amountEtb;
            escrow.Status = EscrowState.HELD;
            escrow.UpdatedAt = DateTime.UtcNow;
        }

        order.EscrowState = EscrowState.HELD;

        // Double-entry record
        await _ledger.RecordTransactionAsync(orderId, "ESCROW_LOCK", $"LOCK-{orderId}", new()
        {
            (LedgerAccountType.BUYER_PAYABLE, LedgerEntryType.DEBIT, amountEtb, order.BuyerId),
            (LedgerAccountType.ESCROW_LIABILITY, LedgerEntryType.CREDIT, amountEtb, null)
        }, ct);

        await _audit.LogAsync(order.BuyerId, "BUYER", "ESCROW_LOCK", "ESCROW", orderId.ToString(), "SUCCESS", $"Locked {amountEtb} ETB", ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return escrow;
    }

    public async Task<EscrowAccount> ReleaseFundsToMerchantAsync(long orderId, long requesterId, CancellationToken ct = default)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var escrow = await _db.EscrowAccounts.FirstOrDefaultAsync(e => e.OrderId == orderId, ct)
            ?? throw new KeyNotFoundException($"Escrow for order {orderId} not found");

        var order = await _db.Orders.FindAsync(new object[] { orderId }, ct)
            ?? throw new KeyNotFoundException($"Order {orderId} not found");

        EscrowStateMachine.ValidateTransition(escrow.Status, EscrowState.RELEASED);

        var releaseAmount = escrow.HeldAmountEtb;
        if (releaseAmount <= 0)
        {
            throw new InvalidOperationException("No funds held to release");
        }

        escrow.HeldAmountEtb = 0;
        escrow.ReleasedAmountEtb = releaseAmount;
        escrow.Status = EscrowState.RELEASED;
        escrow.UpdatedAt = DateTime.UtcNow;
        order.EscrowState = EscrowState.RELEASED;

        // Creator earnings become withdrawable only when the buyer accepts delivery
        // or the inspection window auto-releases escrow. Payment verification alone
        // leaves the commission HELD; disputes therefore cannot release creator funds.
        var creatorCommission = await _db.CreatorCommissions
            .FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (creatorCommission is not null && creatorCommission.Status == "HELD")
        {
            creatorCommission.Status = "AVAILABLE";
            creatorCommission.ReleasedAt = DateTime.UtcNow;
        }

        // Affiliate commission and the referrer's coupon are finalized only
        // after escrow release, never at payment verification time.
        await AffiliateRewardSettlement.FinalizeAsync(_db, order, ct);

        // Award one loyalty point per 100 ETB only after escrow is successfully released.
        var earnedPoints = (int)Math.Floor(releaseAmount / 100m);
        if (earnedPoints > 0 && !await _db.LoyaltyTransactions.AnyAsync(x => x.OrderId == orderId, ct))
        {
            var loyalty = await _db.LoyaltyAccounts.FirstOrDefaultAsync(x => x.UserId == order.BuyerId, ct);
            if (loyalty is null)
            {
                loyalty = new LoyaltyAccount { UserId = order.BuyerId };
                _db.LoyaltyAccounts.Add(loyalty);
            }
            loyalty.PointsBalance = checked(loyalty.PointsBalance + earnedPoints);
            loyalty.UpdatedAt = DateTime.UtcNow;
            _db.LoyaltyTransactions.Add(new LoyaltyTransaction
            {
                UserId = order.BuyerId,
                Points = earnedPoints,
                Reason = "Order settled after escrow release",
                OrderId = orderId,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Split the verified escrow amount between merchant receivable and platform revenue.
        // Legacy orders have zero fee snapshots and retain their previous full-release behavior.
        var platformFee = order.MerchantCommissionEtb + order.BuyerServiceFeeEtb;
        if (platformFee < 0m || platformFee > releaseAmount)
            throw new InvalidOperationException("Marketplace fee snapshot exceeds the escrow amount.");
        var merchantNet = releaseAmount - platformFee;
        var releaseEntries = new List<(LedgerAccountType account, LedgerEntryType type, decimal amount, long? userId)>
        {
            (LedgerAccountType.ESCROW_LIABILITY, LedgerEntryType.DEBIT, releaseAmount, null)
        };
        if (merchantNet > 0m)
            releaseEntries.Add((LedgerAccountType.MERCHANT_RECEIVABLE, LedgerEntryType.CREDIT, merchantNet, order.SellerId));
        if (platformFee > 0m)
            releaseEntries.Add((LedgerAccountType.PLATFORM_FEE, LedgerEntryType.CREDIT, platformFee, null));
        await _ledger.RecordTransactionAsync(orderId, "ESCROW_RELEASE", $"REL-{orderId}", releaseEntries, ct);

        await _audit.LogAsync(requesterId, "MERCHANT/SYSTEM", "ESCROW_RELEASE", "ESCROW", orderId.ToString(), "SUCCESS",
            $"Settled {releaseAmount} ETB: merchant receivable {merchantNet} ETB, platform fees {platformFee} ETB", ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return escrow;
    }

    public async Task<EscrowAccount> RefundFundsToBuyerAsync(long orderId, long requesterId, string reason, CancellationToken ct = default)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var escrow = await _db.EscrowAccounts.FirstOrDefaultAsync(e => e.OrderId == orderId, ct)
            ?? throw new KeyNotFoundException($"Escrow for order {orderId} not found");

        var order = await _db.Orders.FindAsync(new object[] { orderId }, ct)
            ?? throw new KeyNotFoundException($"Order {orderId} not found");

        EscrowStateMachine.ValidateTransition(escrow.Status, EscrowState.REFUNDED);

        var refundAmount = escrow.HeldAmountEtb > 0 ? escrow.HeldAmountEtb : escrow.DisputedAmountEtb;
        if (refundAmount <= 0)
        {
            throw new InvalidOperationException("No funds held/disputed to refund");
        }

        escrow.HeldAmountEtb = 0;
        escrow.DisputedAmountEtb = 0;
        escrow.RefundedAmountEtb = refundAmount;
        escrow.Status = EscrowState.REFUNDED;
        escrow.UpdatedAt = DateTime.UtcNow;
        order.EscrowState = EscrowState.REFUNDED;

        // A refunded order must never leave creator earnings payable.
        var creatorCommission = await _db.CreatorCommissions
            .FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (creatorCommission is not null && creatorCommission.Status is "HELD" or "AVAILABLE")
        {
            creatorCommission.Status = "REVERSED";
        }

        // Refunds reverse affiliate earnings so they can never be paid out.
        var affiliateCommission = await _db.AffiliateCommissions
            .FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (affiliateCommission is not null && affiliateCommission.Status is "HELD" or "AVAILABLE")
        {
            affiliateCommission.Status = "REVERSED";
        }

        // Double-entry record
        await _ledger.RecordTransactionAsync(orderId, "ESCROW_REFUND", $"REF-{orderId}", new()
        {
            (LedgerAccountType.ESCROW_LIABILITY, LedgerEntryType.DEBIT, refundAmount, null),
            (LedgerAccountType.BUYER_PAYABLE, LedgerEntryType.CREDIT, refundAmount, order.BuyerId)
        }, ct);

        await _audit.LogAsync(requesterId, "ARBITER/SYSTEM", "ESCROW_REFUND", "ESCROW", orderId.ToString(), "SUCCESS", $"Refunded {refundAmount} ETB. Reason: {reason}", ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return escrow;
    }

    public async Task<EscrowAccount> FileDisputeAsync(long orderId, long requesterId, string reason, CancellationToken ct = default)
    {
        var escrow = await _db.EscrowAccounts.FirstOrDefaultAsync(e => e.OrderId == orderId, ct)
            ?? throw new KeyNotFoundException($"Escrow for order {orderId} not found");

        var order = await _db.Orders.FindAsync(new object[] { orderId }, ct)
            ?? throw new KeyNotFoundException($"Order {orderId} not found");

        EscrowStateMachine.ValidateTransition(escrow.Status, EscrowState.DISPUTED);

        escrow.DisputedAmountEtb = escrow.HeldAmountEtb;
        escrow.HeldAmountEtb = 0;
        escrow.Status = EscrowState.DISPUTED;
        escrow.UpdatedAt = DateTime.UtcNow;
        order.EscrowState = EscrowState.DISPUTED;

        await _audit.LogAsync(requesterId, "BUYER", "DISPUTE_FILED", "DISPUTE", orderId.ToString(), "SUCCESS", reason, ct);
        await _db.SaveChangesAsync(ct);

        return escrow;
    }
}
