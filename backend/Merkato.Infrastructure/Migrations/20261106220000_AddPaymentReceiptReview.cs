using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261106220000_AddPaymentReceiptReview")]
public sealed class AddPaymentReceiptReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PaymentReceipts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                PaymentTransactionId = table.Column<long>(type: "bigint", nullable: false),
                OrderId = table.Column<long>(type: "bigint", nullable: false),
                BuyerId = table.Column<long>(type: "bigint", nullable: false),
                FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Content = table.Column<byte[]>(type: "bytea", nullable: false),
                Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                TransactionReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                ReviewNote = table.Column<string>(type: "text", nullable: true),
                ReviewedBy = table.Column<long>(type: "bigint", nullable: true),
                ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentReceipts", x => x.Id);
            });

        migrationBuilder.CreateIndex("IX_PaymentTransactions_RailTransactionId", "PaymentTransactions", "RailTransactionId", unique: true, filter: "\"RailTransactionId\" IS NOT NULL");
        migrationBuilder.CreateIndex("IX_PaymentReceipts_OrderId", "PaymentReceipts", "OrderId");
        migrationBuilder.CreateIndex("IX_PaymentReceipts_Sha256", "PaymentReceipts", "Sha256", unique: true);
        migrationBuilder.CreateIndex("IX_PaymentReceipts_PaymentTransactionId_CreatedAt", "PaymentReceipts", new[] { "PaymentTransactionId", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_PaymentReceipts_Status_CreatedAt", "PaymentReceipts", new[] { "Status", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_PaymentReceipts_TransactionReference", "PaymentReceipts", "TransactionReference", unique: true, filter: "\"TransactionReference\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PaymentReceipts");
        migrationBuilder.DropIndex(name: "IX_PaymentTransactions_RailTransactionId", table: "PaymentTransactions");
    }
}
