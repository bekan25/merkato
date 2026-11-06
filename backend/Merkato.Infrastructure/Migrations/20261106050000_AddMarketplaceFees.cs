using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261106050000_AddMarketplaceFees")]
public partial class AddMarketplaceFees : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "ProductSubtotalEtb", table: "Orders", type: "numeric(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "DiscountEtb", table: "Orders", type: "numeric(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "MerchantCommissionRatePercent", table: "Orders", type: "numeric(7,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "MerchantCommissionEtb", table: "Orders", type: "numeric(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "BuyerServiceFeeRatePercent", table: "Orders", type: "numeric(7,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "BuyerServiceFeeEtb", table: "Orders", type: "numeric(18,4)", nullable: false, defaultValue: 0m);

        migrationBuilder.CreateTable(
            name: "MarketplaceFeeSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                MerchantCommissionRatePercent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                BuyerServiceFeeRatePercent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_MarketplaceFeeSettings", x => x.Id));

        migrationBuilder.InsertData(
            table: "MarketplaceFeeSettings",
            columns: new[] { "Id", "MerchantCommissionRatePercent", "BuyerServiceFeeRatePercent", "IsEnabled", "UpdatedAt", "UpdatedByUserId" },
            values: new object[] { 1, 1.0000m, 0.5000m, true, DateTime.UtcNow, null });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MarketplaceFeeSettings");
        migrationBuilder.DropColumn(name: "ProductSubtotalEtb", table: "Orders");
        migrationBuilder.DropColumn(name: "DiscountEtb", table: "Orders");
        migrationBuilder.DropColumn(name: "MerchantCommissionRatePercent", table: "Orders");
        migrationBuilder.DropColumn(name: "MerchantCommissionEtb", table: "Orders");
        migrationBuilder.DropColumn(name: "BuyerServiceFeeRatePercent", table: "Orders");
        migrationBuilder.DropColumn(name: "BuyerServiceFeeEtb", table: "Orders");
    }
}
