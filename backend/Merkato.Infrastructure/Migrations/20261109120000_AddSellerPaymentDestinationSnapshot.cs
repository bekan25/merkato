using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261109120000_AddSellerPaymentDestinationSnapshot")]
public sealed class AddSellerPaymentDestinationSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SellerPaymentAccountHolderName",
            table: "Orders",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SellerPaymentAccountIdentifier",
            table: "Orders",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SellerPaymentBankName",
            table: "Orders",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SellerPaymentInstructions",
            table: "Orders",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "SellerPaymentDestinationCapturedAt",
            table: "Orders",
            type: "timestamp with time zone",
            nullable: true);

        // Preserve the currently configured destination for historical seller-direct orders.
        // If no active destination exists, leave the snapshot empty and fail closed.
        migrationBuilder.Sql("""
            UPDATE "Orders" AS o
            SET
                "SellerPaymentAccountHolderName" = s."AccountHolderName",
                "SellerPaymentAccountIdentifier" = s."AccountIdentifier",
                "SellerPaymentBankName" = s."BankName",
                "SellerPaymentInstructions" = s."Instructions",
                "SellerPaymentDestinationCapturedAt" = COALESCE(s."UpdatedAt", o."CreatedAt")
            FROM "SellerPaymentAccounts" AS s
            WHERE o."PaymentMode" = 'SELLER_DIRECT'
              AND o."SellerId" = s."SellerId"
              AND o."PaymentRail" = s."PaymentRail"
              AND s."IsActive" = TRUE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SellerPaymentAccountHolderName", table: "Orders");
        migrationBuilder.DropColumn(name: "SellerPaymentAccountIdentifier", table: "Orders");
        migrationBuilder.DropColumn(name: "SellerPaymentBankName", table: "Orders");
        migrationBuilder.DropColumn(name: "SellerPaymentInstructions", table: "Orders");
        migrationBuilder.DropColumn(name: "SellerPaymentDestinationCapturedAt", table: "Orders");
    }
}
