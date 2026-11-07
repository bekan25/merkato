using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Merkato.Application.Interfaces;
using Merkato.Domain.Entities;
using Merkato.Domain.Enums;
using Merkato.Infrastructure.Persistence;
using Merkato.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Merkato.Api.Controllers;

[ApiController]
[Route("api/payment-receipts")]
public sealed class PaymentReceiptsController : ControllerBase
{
    private const long MaxReceiptBytes = 5 * 1024 * 1024;
    private readonly MerkatoDbContext _db;
    private readonly IEscrowService _escrow;
    private readonly IAuditLogService _audit;
    private readonly IReceiptOcrService _ocr;

    public PaymentReceiptsController(MerkatoDbContext db, IEscrowService escrow, IAuditLogService audit, IReceiptOcrService ocr)
    {
        _db = db;
        _escrow = escrow;
        _audit = audit;
        _ocr = ocr;
    }

    [HttpPost]
    [Authorize(Roles = "BUYER,ADMIN")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("payment-receipts")]
    [RequestSizeLimit(MaxReceiptBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(
        [FromForm] long orderId,
        [FromForm] IFormFile? receipt,
        [FromForm] string? transactionReference,
        CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var buyerId))
            return Unauthorized();
        if (orderId <= 0 || receipt is null || receipt.Length <= 0 || receipt.Length > MaxReceiptBytes)
            return BadRequest(new { error = "Provide a valid order and a receipt image up to 5 MB." });

        var extension = Path.GetExtension(receipt.FileName).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png")
            return BadRequest(new { error = "Only JPEG and PNG receipts are accepted." });
        if (receipt.ContentType is not "image/jpeg" and not "image/png")
            return BadRequest(new { error = "Receipt content type must be image/jpeg or image/png." });

        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null) return NotFound(new { error = "Order not found." });
        if (order.BuyerId != buyerId && !User.IsInRole("ADMIN")) return Forbid();
        if (order.EscrowState != EscrowState.AWAITING_PAYMENT)
            return Conflict(new { error = "This order is not awaiting payment." });

        var payment = await _db.PaymentTransactions
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (payment is null || payment.Status is not PaymentStatus.PENDING and not PaymentStatus.INITIATED)
            return Conflict(new { error = "Initiate a payment for this order before uploading a receipt." });
        if (payment.AmountEtb != order.TotalAmountEtb)
            return Conflict(new { error = "Payment transaction amount does not match the order total." });

        await using var stream = new MemoryStream((int)receipt.Length);
        await receipt.CopyToAsync(stream, ct);
        var bytes = stream.ToArray();
        if (!HasValidImageSignature(bytes, extension))
            return BadRequest(new { error = "The uploaded file does not contain a valid JPEG or PNG signature." });

        var ocr = await _ocr.ReadAsync(bytes, extension, order.TotalAmountEtb, ct);
        var reference = string.IsNullOrWhiteSpace(transactionReference) ? ocr.TransactionReference : transactionReference.Trim().ToUpperInvariant();
        if (reference?.Length > 150)
            return BadRequest(new { error = "Transaction reference must be 150 characters or fewer." });
        if (reference is not null && await _db.PaymentReceipts.AnyAsync(x => x.TransactionReference == reference, ct))
            return Conflict(new { error = "This transaction reference has already been submitted." });

        var contentType = extension == ".png" ? "image/png" : "image/jpeg";
        var safeName = Path.GetFileName(receipt.FileName);
        var record = new PaymentReceipt
        {
            PaymentTransactionId = payment.Id,
            OrderId = order.Id,
            BuyerId = order.BuyerId,
            FileName = safeName.Length > 255 ? safeName[^255..] : safeName,
            ContentType = contentType,
            Content = bytes,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            TransactionReference = reference,
            Status = "PENDING_REVIEW",
            CreatedAt = DateTime.UtcNow
        };
        _db.PaymentReceipts.Add(record);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { error = "This transaction reference or receipt submission already exists." });
        }

        await _audit.LogAsync(buyerId, "BUYER", "PAYMENT_RECEIPT_UPLOADED",
            "PAYMENT_RECEIPT", record.Id.ToString(), "SUCCESS",
            $"Order {order.OrderNumber}; SHA256 {record.Sha256}", ct: ct);

        return Accepted(new
        {
            receiptId = record.Id,
            orderId = record.OrderId,
            paymentTransactionId = record.PaymentTransactionId,
            expectedAmountEtb = payment.AmountEtb,
            transactionReference = record.TransactionReference,
            status = record.Status,
            ocrStatus = ocr.Status,
            ocrProvider = ocr.Provider,
            ocrTransactionReference = ocr.TransactionReference,
            ocrAmountEtb = ocr.AmountEtb,
            ocrTransactionDate = ocr.TransactionDate,
            ocrError = ocr.Error,
            message = "Receipt received. OCR extracted visible details; payment remains pending until an administrator independently confirms the transfer."
        });
    }

    [HttpGet("mine")]
    [Authorize(Roles = "BUYER,ADMIN")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var buyerId))
            return Unauthorized();
        var query = _db.PaymentReceipts.AsNoTracking();
        if (!User.IsInRole("ADMIN")) query = query.Where(x => x.BuyerId == buyerId);
        var records = await query.OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new { x.Id, x.OrderId, x.PaymentTransactionId, x.TransactionReference, x.Status, x.ReviewNote, x.CreatedAt, x.ReviewedAt })
            .ToListAsync(ct);
        return Ok(records);
    }

    [HttpGet("admin/pending")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        var records = await (
            from receipt in _db.PaymentReceipts.AsNoTracking()
            join order in _db.Orders.AsNoTracking() on receipt.OrderId equals order.Id
            join payment in _db.PaymentTransactions.AsNoTracking() on receipt.PaymentTransactionId equals payment.Id
            where receipt.Status == "PENDING_REVIEW" && order.PaymentMode != "SELLER_DIRECT"
            orderby receipt.CreatedAt
            select new
            {
                receipt.Id, receipt.OrderId, order.OrderNumber, receipt.BuyerId,
                receipt.PaymentTransactionId, expectedAmountEtb = payment.AmountEtb,
                receipt.TransactionReference, receipt.FileName, receipt.ContentType,
                receipt.Sha256, receipt.Status, receipt.CreatedAt, receipt.Content
            }).Take(100).ToListAsync(ct);
        var result = new List<object>(records.Count);
        foreach (var item in records)
        {
            var extension = item.ContentType == "image/png" ? ".png" : ".jpg";
            var ocr = await _ocr.ReadAsync(item.Content, extension, item.expectedAmountEtb, ct);
            result.Add(new
            {
                item.Id, item.OrderId, item.OrderNumber, item.BuyerId, item.PaymentTransactionId,
                item.expectedAmountEtb, item.TransactionReference, item.FileName, item.ContentType,
                item.Sha256, item.Status, item.CreatedAt, ocrStatus = ocr.Status,
                ocrProvider = ocr.Provider, ocrTransactionReference = ocr.TransactionReference,
                ocrAmountEtb = ocr.AmountEtb, ocrTransactionDate = ocr.TransactionDate, ocrError = ocr.Error
            });
        }
        return Ok(result);
    }

    [HttpGet("admin/{id:long}/file")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> GetFile(long id, CancellationToken ct)
    {
        var receipt = await _db.PaymentReceipts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (receipt is null) return NotFound();
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(receipt.Content, receipt.ContentType);
    }

    [HttpPost("admin/{id:long}/review")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Review(long id, [FromBody] ReviewPaymentReceiptRequest request, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            return Unauthorized();
        var decision = request.Decision?.Trim().ToUpperInvariant();
        var note = request.Note?.Trim();
        if (decision is not "APPROVE" and not "REJECT")
            return BadRequest(new { error = "Decision must be APPROVE or REJECT." });
        if (string.IsNullOrWhiteSpace(note) || note.Length > 1000)
            return BadRequest(new { error = "A review note of 1 to 1000 characters is required." });

        var receipt = await _db.PaymentReceipts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (receipt is null) return NotFound();
        if (receipt.Status != "PENDING_REVIEW")
            return Conflict(new { error = "This receipt has already been reviewed." });

        var payment = await _db.PaymentTransactions.FirstOrDefaultAsync(x => x.Id == receipt.PaymentTransactionId, ct);
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == receipt.OrderId, ct);
        if (payment is null || order is null) return Conflict(new { error = "Linked payment or order is missing." });
        if (order.PaymentMode == "SELLER_DIRECT")
            return Conflict(new { error = "Seller-direct receipts must be reviewed by the seller, not funded through MERKATO escrow." });

        if (decision == "REJECT")
        {
            receipt.Status = "REJECTED";
            receipt.ReviewNote = note;
            receipt.ReviewedBy = adminId;
            receipt.ReviewedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(adminId, "ADMIN", "PAYMENT_RECEIPT_REJECTED",
                "PAYMENT_RECEIPT", receipt.Id.ToString(), "SUCCESS", note, ct: ct);
            return Ok(new { receiptId = receipt.Id, status = receipt.Status, paymentStatus = payment.Status.ToString() });
        }

        var reference = string.IsNullOrWhiteSpace(request.TransactionReference)
            ? receipt.TransactionReference
            : request.TransactionReference.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 150)
            return BadRequest(new { error = "A verified bank/provider transaction reference is required for approval." });
        if (order.EscrowState != EscrowState.AWAITING_PAYMENT ||
            payment.Status is not PaymentStatus.PENDING and not PaymentStatus.INITIATED ||
            payment.AmountEtb != order.TotalAmountEtb)
            return Conflict(new { error = "Order or payment is no longer eligible for manual verification." });
        if (await _db.PaymentTransactions.AnyAsync(x => x.Id != payment.Id && x.RailTransactionId == reference, ct))
            return Conflict(new { error = "That provider transaction reference is already linked to another payment." });
        if (await _db.PaymentReceipts.AnyAsync(x => x.Id != receipt.Id && x.TransactionReference == reference, ct))
            return Conflict(new { error = "That transaction reference has already been used on another receipt." });

        receipt.TransactionReference = reference;
        receipt.Status = "VERIFIED";
        receipt.ReviewNote = note;
        receipt.ReviewedBy = adminId;
        receipt.ReviewedAt = DateTime.UtcNow;
        payment.RailTransactionId = reference;
        payment.Status = PaymentStatus.VERIFIED;
        payment.WebhookVerified = false; // Explicitly records manual verification, not a signed provider webhook.
        payment.RawPayloadHash = receipt.Sha256;
        payment.VerifiedAt = DateTime.UtcNow;

        try
        {
            await _escrow.LockFundsAsync(order.Id, payment.AmountEtb, ct);
            await _audit.LogAsync(adminId, "ADMIN", "PAYMENT_MANUALLY_VERIFIED",
                "PAYMENT", payment.Id.ToString(), "SUCCESS",
                $"Receipt {receipt.Id}; reference {reference}; {note}", ct: ct);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { error = "This provider transaction reference has already been used." });
        }

        return Ok(new
        {
            receiptId = receipt.Id,
            paymentTransactionId = payment.Id,
            orderId = order.Id,
            status = receipt.Status,
            paymentStatus = payment.Status.ToString(),
            escrowState = order.EscrowState.ToString(),
            webhookVerified = payment.WebhookVerified
        });
    }

    [HttpGet("seller/pending")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> SellerPending(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var sellerId))
            return Unauthorized();

        var records = await (
            from receipt in _db.PaymentReceipts.AsNoTracking()
            join order in _db.Orders.AsNoTracking() on receipt.OrderId equals order.Id
            join payment in _db.PaymentTransactions.AsNoTracking() on receipt.PaymentTransactionId equals payment.Id
            where order.SellerId == sellerId && order.PaymentMode == "SELLER_DIRECT" && receipt.Status == "PENDING_REVIEW"
            orderby receipt.CreatedAt
            select new
            {
                receipt.Id, receipt.OrderId, order.OrderNumber, receipt.BuyerId,
                receipt.PaymentTransactionId, expectedAmountEtb = payment.AmountEtb,
                receipt.TransactionReference, receipt.FileName, receipt.ContentType,
                receipt.Sha256, receipt.Status, receipt.CreatedAt
            }).Take(100).ToListAsync(ct);
        return Ok(records);
    }

    [HttpGet("seller/{id:long}/file")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> GetSellerFile(long id, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var sellerId))
            return Unauthorized();
        var receipt = await (
            from item in _db.PaymentReceipts.AsNoTracking()
            join order in _db.Orders.AsNoTracking() on item.OrderId equals order.Id
            where item.Id == id && order.SellerId == sellerId
            select item).FirstOrDefaultAsync(ct);
        if (receipt is null) return NotFound();
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(receipt.Content, receipt.ContentType);
    }

    [HttpPost("seller/{id:long}/review")]
    [Authorize(Roles = "MERCHANT")]
    public async Task<IActionResult> SellerReview(long id, [FromBody] ReviewPaymentReceiptRequest request, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var sellerId))
            return Unauthorized();
        var decision = request.Decision?.Trim().ToUpperInvariant();
        var note = request.Note?.Trim();
        if (decision is not "APPROVE" and not "REJECT")
            return BadRequest(new { error = "Decision must be APPROVE or REJECT." });
        if (string.IsNullOrWhiteSpace(note) || note.Length > 1000)
            return BadRequest(new { error = "A review note of 1 to 1000 characters is required." });

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var receipt = await _db.PaymentReceipts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (receipt is null) return NotFound();
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == receipt.OrderId, ct);
        if (order is null) return Conflict(new { error = "Linked order is missing." });
        if (order.SellerId != sellerId) return Forbid();
        if (order.PaymentMode != "SELLER_DIRECT")
            return Conflict(new { error = "This receipt belongs to an escrow-mode order and must follow the existing admin review workflow." });
        if (!await _db.SellerPaymentAccounts.AnyAsync(x =>
            x.SellerId == sellerId && x.PaymentRail == order.PaymentRail && x.IsActive, ct))
            return Conflict(new { error = "Configure and activate the matching seller payment account before reviewing this transfer." });
        if (order.EscrowState != EscrowState.AWAITING_PAYMENT)
            return Conflict(new { error = "This order is not awaiting seller payment confirmation." });
        if (receipt.Status != "PENDING_REVIEW")
            return Conflict(new { error = "This receipt has already been reviewed." });
        var payment = await _db.PaymentTransactions.FirstOrDefaultAsync(x => x.Id == receipt.PaymentTransactionId, ct);
        if (payment is null || payment.OrderId != order.Id ||
            payment.Status is not PaymentStatus.PENDING and not PaymentStatus.INITIATED ||
            payment.AmountEtb != order.TotalAmountEtb)
            return Conflict(new { error = "Linked payment is no longer eligible for seller review." });

        if (decision == "REJECT")
        {
            receipt.Status = "REJECTED";
            receipt.ReviewNote = note;
            receipt.ReviewedBy = sellerId;
            receipt.ReviewedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(sellerId, "MERCHANT", "SELLER_PAYMENT_RECEIPT_REJECTED",
                "PAYMENT_RECEIPT", receipt.Id.ToString(), "SUCCESS", note, ct: ct);
            await tx.CommitAsync(ct);
            return Ok(new { receiptId = receipt.Id, status = receipt.Status });
        }

        var reference = string.IsNullOrWhiteSpace(request.TransactionReference)
            ? receipt.TransactionReference
            : request.TransactionReference.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 150)
            return BadRequest(new { error = "Enter the transaction reference after checking the actual credit in your bank/provider account." });
        if (await _db.PaymentTransactions.AnyAsync(x => x.Id != payment.Id && x.RailTransactionId == reference, ct))
            return Conflict(new { error = "That provider transaction reference is already linked to another payment." });
        if (await _db.PaymentReceipts.AnyAsync(x => x.Id != receipt.Id && x.TransactionReference == reference, ct))
            return Conflict(new { error = "That transaction reference has already been used on another receipt." });

        receipt.TransactionReference = reference;
        receipt.Status = "SELLER_CONFIRMED";
        receipt.ReviewNote = note;
        receipt.ReviewedBy = sellerId;
        receipt.ReviewedAt = DateTime.UtcNow;
        payment.RailTransactionId = reference;
        payment.Status = PaymentStatus.VERIFIED;
        payment.WebhookVerified = false;
        payment.RawPayloadHash = receipt.Sha256;
        payment.VerifiedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(sellerId, "MERCHANT", "SELLER_DIRECT_PAYMENT_CONFIRMED",
            "PAYMENT", payment.Id.ToString(), "SUCCESS",
            $"Receipt {receipt.Id}; reference {reference}; {note}. Seller-confirmed; no platform escrow.", ct: ct);
        await tx.CommitAsync(ct);

        return Ok(new
        {
            receiptId = receipt.Id,
            paymentTransactionId = payment.Id,
            orderId = order.Id,
            receiptStatus = receipt.Status,
            paymentStatus = payment.Status.ToString(),
            escrowState = order.EscrowState.ToString(),
            message = "Seller confirmation recorded. MERKATO did not receive or hold the funds."
        });
    }

    private static bool HasValidImageSignature(byte[] bytes, string extension)
    {
        if (extension == ".png")
            return bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
    }
}

public sealed record ReviewPaymentReceiptRequest(string Decision, string Note, string? TransactionReference);
