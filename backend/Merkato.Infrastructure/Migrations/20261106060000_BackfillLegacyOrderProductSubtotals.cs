using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261106060000_BackfillLegacyOrderProductSubtotals")]
public partial class BackfillLegacyOrderProductSubtotals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Older orders predate fee snapshots. Preserve their historic creator/affiliate
        // commission basis while leaving merchant and buyer fee snapshots at zero.
        migrationBuilder.Sql("""
            UPDATE "Orders"
            SET "ProductSubtotalEtb" = GREATEST("TotalAmountEtb" - "DeliveryFeeEtb", 0),
                "DiscountEtb" = 0
            WHERE "ProductSubtotalEtb" = 0
              AND "TotalAmountEtb" > "DeliveryFeeEtb";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally irreversible: clearing these values would discard the backfilled
        // historical commission basis and could affect pending payments/webhooks.
    }
}
