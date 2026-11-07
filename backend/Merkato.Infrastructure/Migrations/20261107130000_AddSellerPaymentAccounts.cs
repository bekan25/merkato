using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261107130000_AddSellerPaymentAccounts")]
public sealed class AddSellerPaymentAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SellerPaymentAccounts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SellerId = table.Column<long>(type: "bigint", nullable: false),
                PaymentRail = table.Column<int>(type: "integer", nullable: false),
                AccountHolderName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                AccountIdentifier = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                Instructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SellerPaymentAccounts", x => x.Id);
                table.ForeignKey(
                    name: "FK_SellerPaymentAccounts_Users_SellerId",
                    column: x => x.SellerId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SellerPaymentAccounts_SellerId_PaymentRail",
            table: "SellerPaymentAccounts",
            columns: new[] { "SellerId", "PaymentRail" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SellerPaymentAccounts");
    }
}
