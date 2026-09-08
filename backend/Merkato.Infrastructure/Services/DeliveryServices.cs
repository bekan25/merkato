using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Domain.StateMachines;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Merkato.Infrastructure.Services;

public class DeliveryService : IDeliveryService
{
    private readonly MerkatoDbContext _db;
    private readonly IInspectionService _inspection;
    private readonly IAuditLogService _audit;

    public DeliveryService(
        MerkatoDbContext db,
        IInspectionService inspection,
        IAuditLogService audit)
    {
        _db = db;
        _inspection = inspection;
        _audit = audit;
    }

    public async Task<Delivery> CreateDeliveryAsync(
        long orderId,
        string courierType,
        decimal feeEtb,
        string rawOtp,
        CancellationToken ct = default)
    {
        // Generate secure salt
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var saltHex = Convert.ToHexString(saltBytes);

        // Generate unique delivery ID
        var deliveryId =
            $"DEL-{orderId}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";

        // Hash OTP
        var hash = ComputeOtpHash(
            rawOtp,
            saltHex,
            orderId.ToString(),
            deliveryId);

        var delivery = new Delivery
        {
            DeliveryId = deliveryId,
            OrderId = orderId,
            CourierType = courierType,
            FeeEtb = feeEtb,
            OtpHash = hash,
            OtpSalt = saltHex,
            Status = DeliveryStatus.CREATED,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var tracker = new OtpAttemptTracker
        {
            DeliveryId = deliveryId,
            FailedAttempts = 0,
            IsConsumed = false,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Deliveries.Add(delivery);
        _db.OtpAttemptTrackers.Add(tracker);

        await _db.SaveChangesAsync(ct);

        return delivery;
    }

    public async Task<Delivery> VerifyOtpAndCompleteHandoverAsync(
        string deliveryId,
        long requesterId,
        string rawOtp,
        CancellationToken ct = default)
    {
        // ---------------------------------------------------------
        // 1. Find delivery
        // ---------------------------------------------------------

        var delivery = await _db.Deliveries
            .FirstOrDefaultAsync(
                d => d.DeliveryId == deliveryId,
                ct)
            ?? throw new KeyNotFoundException(
                $"Delivery {deliveryId} not found");

        // ---------------------------------------------------------
        // 2. Find OTP tracker
        // ---------------------------------------------------------

        var tracker = await _db.OtpAttemptTrackers
            .FirstOrDefaultAsync(
                t => t.DeliveryId == deliveryId,
                ct)
            ?? throw new KeyNotFoundException(
                $"OTP tracker for {deliveryId} not found");

        // ---------------------------------------------------------
        // 3. Check whether OTP was already consumed
        // ---------------------------------------------------------

        if (tracker.IsConsumed)
        {
            throw new InvalidOperationException(
                "OTP has already been consumed");
        }

        // ---------------------------------------------------------
        // 4. Check OTP lock
        // ---------------------------------------------------------

        var now = DateTime.UtcNow;

        if (tracker.LockedUntil.HasValue &&
            tracker.LockedUntil.Value > now)
        {
            var remainingSecs =
                (int)(tracker.LockedUntil.Value - now).TotalSeconds;

            throw new InvalidOperationException(
                $"OTP verification is locked. Try again in {remainingSecs} seconds");
        }

        // ---------------------------------------------------------
        // 5. Verify OTP hash
        // ---------------------------------------------------------

        var computedHash = ComputeOtpHash(
            rawOtp,
            delivery.OtpSalt,
            delivery.OrderId.ToString(),
            delivery.DeliveryId);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(delivery.OtpHash)))
        {
            tracker.FailedAttempts++;

            if (tracker.FailedAttempts >= 3)
            {
                tracker.LockedUntil = now.AddMinutes(15);
            }

            tracker.UpdatedAt = now;

            await _db.SaveChangesAsync(ct);

            throw new UnauthorizedAccessException(
                $"Invalid OTP code. Failed attempts: {tracker.FailedAttempts}/3");
        }

        // ---------------------------------------------------------
        // 6. OTP verified successfully
        // ---------------------------------------------------------

        tracker.IsConsumed = true;
        tracker.ConsumedAt = now;
        tracker.FailedAttempts = 0;
        tracker.LockedUntil = null;
        tracker.UpdatedAt = now;

        // ---------------------------------------------------------
        // 7. Move delivery through valid state transitions
        //
        // CREATED
        //    ↓
        // ASSIGNED_COURIER
        //    ↓
        // PICKED_UP
        //    ↓
        // OUT_FOR_DELIVERY
        //    ↓
        // DELIVERED
        // ---------------------------------------------------------

        if (delivery.Status == DeliveryStatus.CREATED)
        {
            DeliveryStateMachine.ValidateTransition(
                delivery.Status,
                DeliveryStatus.ASSIGNED_COURIER);

            delivery.Status =
                DeliveryStatus.ASSIGNED_COURIER;
        }

        if (delivery.Status == DeliveryStatus.ASSIGNED_COURIER)
        {
            DeliveryStateMachine.ValidateTransition(
                delivery.Status,
                DeliveryStatus.PICKED_UP);

            delivery.Status =
                DeliveryStatus.PICKED_UP;
        }

        if (delivery.Status == DeliveryStatus.PICKED_UP)
        {
            DeliveryStateMachine.ValidateTransition(
                delivery.Status,
                DeliveryStatus.OUT_FOR_DELIVERY);

            delivery.Status =
                DeliveryStatus.OUT_FOR_DELIVERY;
        }

        // ---------------------------------------------------------
        // 8. Final transition to DELIVERED
        // ---------------------------------------------------------

        DeliveryStateMachine.ValidateTransition(
            delivery.Status,
            DeliveryStatus.DELIVERED);

        delivery.Status = DeliveryStatus.DELIVERED;
        delivery.DeliveredAt = now;
        delivery.UpdatedAt = now;

        // ---------------------------------------------------------
        // 9. Update Order delivery status
        // ---------------------------------------------------------

        var order = await _db.Orders.FindAsync(
            new object[] { delivery.OrderId },
            ct);

        if (order != null)
        {
            order.DeliveryStatus =
                DeliveryStatus.DELIVERED;
        }

        // ---------------------------------------------------------
        // 10. Start 48-hour inspection window
        //
        // IMPORTANT:
        // Do NOT release escrow here.
        // Escrow remains in inspection until buyer accepts
        // or the inspection expires.
        // ---------------------------------------------------------

        await _inspection.StartInspectionAsync(
            delivery.OrderId,
            ct);

        // ---------------------------------------------------------
        // 11. Audit log
        // ---------------------------------------------------------

        await _audit.LogAsync(
            requesterId,
            "COURIER/BUYER",
            "OTP_VERIFIED",
            "DELIVERY",
            deliveryId,
            "SUCCESS",
            "Doorstep handover verified",
            ct);

        // ---------------------------------------------------------
        // 12. Save everything
        // ---------------------------------------------------------

        await _db.SaveChangesAsync(ct);

        return delivery;
    }

    public async Task<Delivery> UpdateDeliveryStatusAsync(
        string deliveryId,
        DeliveryStatus nextStatus,
        CancellationToken ct = default)
    {
        var delivery = await _db.Deliveries
            .FirstOrDefaultAsync(
                d => d.DeliveryId == deliveryId,
                ct)
            ?? throw new KeyNotFoundException(
                $"Delivery {deliveryId} not found");

        // Validate state machine transition
        DeliveryStateMachine.ValidateTransition(
            delivery.Status,
            nextStatus);

        delivery.Status = nextStatus;
        delivery.UpdatedAt = DateTime.UtcNow;

        // If delivery becomes DELIVERED,
        // start inspection window.
        if (nextStatus == DeliveryStatus.DELIVERED)
        {
            delivery.DeliveredAt = DateTime.UtcNow;

            await _inspection.StartInspectionAsync(
                delivery.OrderId,
                ct);

            var order = await _db.Orders.FindAsync(
                new object[] { delivery.OrderId },
                ct);

            if (order != null)
            {
                order.DeliveryStatus =
                    DeliveryStatus.DELIVERED;
            }
        }

        await _db.SaveChangesAsync(ct);

        return delivery;
    }

    // -------------------------------------------------------------
    // OTP HASH
    // -------------------------------------------------------------

    private static string ComputeOtpHash(
        string rawOtp,
        string saltHex,
        string orderId,
        string deliveryId)
    {
        var input =
            $"{saltHex}:{orderId}:{deliveryId}:{rawOtp}";

        var hashBytes =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(input));

        return Convert.ToHexString(hashBytes);
    }
}