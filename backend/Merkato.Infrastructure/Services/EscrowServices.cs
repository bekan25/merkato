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

        var escrow = await _db.EscrowAccounts.FirstOrDefaultAsync(e => e.OrderId == orderId, ct);
        // Replayed verified webhooks must not reserve stock or create a second ledger lock.
        if (escrow?.Status == EscrowState.HELD)
        {
            return escrow;
        }

        if (escrow == null)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.SellerId == order.SellerId && p.Name == order.ProductTitle, ct);
            if (product is not null)
            {
                var listing = await _db.ProductListingDetails.FirstOrDefaultAsync(x => x.ProductId == product.Id, ct);
                if (listing is not null)
                {
                    if (listing.StockQuantity < 1)
                    {
                        throw new InvalidOperationException("This product is out of stock.");
                    }
                    listing.StockQuantity -= 1;
                    listing.UpdatedAt = DateTime.UtcNow;
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

        // Double-entry record
        await _ledger.RecordTransactionAsync(orderId, "ESCROW_RELEASE", $"REL-{orderId}", new()
        {
            (LedgerAccountType.ESCROW_LIABILITY, LedgerEntryType.DEBIT, releaseAmount, null),
            (LedgerAccountType.MERCHANT_RECEIVABLE, LedgerEntryType.CREDIT, releaseAmount, order.SellerId)
        }, ct);

        await _audit.LogAsync(requesterId, "MERCHANT/SYSTEM", "ESCROW_RELEASE", "ESCROW", orderId.ToString(), "SUCCESS", $"Released {releaseAmount} ETB", ct);
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
