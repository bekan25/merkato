using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Merkato.Tests;

public sealed class PaymentReceiptModelTests
{
    [Fact]
    public void PaymentReceipt_MapsPrivateBinaryContentAndUniqueReferenceIndexes()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseNpgsql("Host=localhost;Database=merkato_model_test;Username=merkato_model_test;Password=not-used")
            .Options;
        using var db = new MerkatoDbContext(options);

        var receipt = db.Model.FindEntityType(typeof(PaymentReceipt));
        Assert.NotNull(receipt);
        Assert.Equal("PaymentReceipts", receipt!.GetTableName());
        Assert.Equal("bytea", receipt.FindProperty(nameof(PaymentReceipt.Content))!.GetColumnType());
        Assert.Contains(receipt.GetIndexes(), index =>
            index.IsUnique && index.Properties.Any(p => p.Name == nameof(PaymentReceipt.Sha256)));
        Assert.Contains(receipt.GetIndexes(), index =>
            index.IsUnique && index.Properties.Any(p => p.Name == nameof(PaymentReceipt.TransactionReference)));
        Assert.Contains(receipt.GetIndexes(), index =>
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(PaymentReceipt.Status), nameof(PaymentReceipt.CreatedAt) }));
    }

    [Fact]
    public void PaymentTransaction_EnforcesUniqueNonNullProviderReferenceInModel()
    {
        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseNpgsql("Host=localhost;Database=merkato_model_test;Username=merkato_model_test;Password=not-used")
            .Options;
        using var db = new MerkatoDbContext(options);

        var payment = db.Model.FindEntityType(typeof(PaymentTransaction));
        Assert.NotNull(payment);
        Assert.Contains(payment!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Any(p => p.Name == nameof(PaymentTransaction.RailTransactionId)));
    }
}
