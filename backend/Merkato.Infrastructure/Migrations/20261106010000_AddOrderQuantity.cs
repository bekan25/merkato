using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261106010000_AddOrderQuantity")]
public sealed class AddOrderQuantity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Quantity",
            table: "Orders",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<decimal>(
            name: "UnitPriceEtb",
            table: "Orders",
            type: "numeric(18,4)",
            nullable: false,
            defaultValue: 0m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "UnitPriceEtb", table: "Orders");
        migrationBuilder.DropColumn(name: "Quantity", table: "Orders");
    }
}