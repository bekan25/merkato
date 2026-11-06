using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Merkato.Domain.Entities;

/// <summary>
/// Private buyer-submitted payment evidence. Receipt bytes stay in PostgreSQL
/// and are never exposed through public/static file hosting.
/// </summary>
public sealed class PaymentReceipt
{
    public long Id { get; set; }
    public long PaymentTransactionId { get; set; }
    public long OrderId { get; set; }
    public long BuyerId { get; set; }

    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Column(TypeName = "bytea")]
    public byte[] Content { get; set; } = Array.Empty<byte>();

    [MaxLength(64)]
    public string Sha256 { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? TransactionReference { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "PENDING_REVIEW";

    public string? ReviewNote { get; set; }
    public long? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
