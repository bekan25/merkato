using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261109100000_AddSellerChapaAccounts")]
public sealed class AddSellerChapaAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SellerChapaAccounts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SellerId = table.Column<long>(type: "bigint", nullable: false),
                ChapaSubaccountId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                ProviderReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                BusinessName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                AccountName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                BankCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                AccountNumberLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                OnboardingStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValue: "NOT_STARTED"),
                ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SellerChapaAccounts", x => x.Id);
                table.ForeignKey(
                    name: "FK_SellerChapaAccounts_Users_SellerId",
                    column: x => x.SellerId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SellerChapaAccounts_SellerId",
            table: "SellerChapaAccounts",
            column: "SellerId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SellerChapaAccounts_ChapaSubaccountId",
            table: "SellerChapaAccounts",
            column: "ChapaSubaccountId",
            unique: true,
            filter: "\"ChapaSubaccountId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_SellerChapaAccounts_ProviderReference",
            table: "SellerChapaAccounts",
            column: "ProviderReference",
            unique: true,
            filter: "\"ProviderReference\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SellerChapaAccounts");
    }
}
