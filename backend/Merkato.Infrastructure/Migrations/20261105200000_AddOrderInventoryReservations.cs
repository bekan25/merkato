using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261105200000_AddOrderInventoryReservations")]
public sealed class AddOrderInventoryReservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "InventoryReservationState",
            table: "Orders",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "InventoryReservationExpiresAt",
            table: "Orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Orders_InventoryReservationState_InventoryReservationExpiresAt",
            table: "Orders",
            columns: new[] { "InventoryReservationState", "InventoryReservationExpiresAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Orders_InventoryReservationState_InventoryReservationExpiresAt",
            table: "Orders");
        migrationBuilder.DropColumn(name: "InventoryReservationExpiresAt", table: "Orders");
        migrationBuilder.DropColumn(name: "InventoryReservationState", table: "Orders");
    }
}
