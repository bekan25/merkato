using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261105180000_AddReservedInventory")]
public sealed class AddReservedInventory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ReservedQuantity",
            table: "ProductListingDetails",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddCheckConstraint(
            name: "CK_ProductListingDetails_StockNonNegative",
            table: "ProductListingDetails",
            sql: "\"StockQuantity\" >= 0 AND \"ReservedQuantity\" >= 0 AND \"ReservedQuantity\" <= \"StockQuantity\"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_ProductListingDetails_StockNonNegative",
            table: "ProductListingDetails");
        migrationBuilder.DropColumn(
            name: "ReservedQuantity",
            table: "ProductListingDetails");
    }
}
