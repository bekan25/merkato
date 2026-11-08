using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261105190000_AddOrderProductReference")]
public sealed class AddOrderProductReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "ProductId",
            table: "Orders",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Orders_ProductId",
            table: "Orders",
            column: "ProductId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Orders_ProductId", table: "Orders");
        migrationBuilder.DropColumn(name: "ProductId", table: "Orders");
    }
}
