using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261107040000_BackfillLegacyOrderProductSubtotals")]
public partial class BackfillLegacyOrderProductSubtotals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Legacy orders were created before marketplace fee snapshots existed.
        // Preserve their historic creator/affiliate commission basis without
        // retroactively charging merchant or buyer fees.
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
        // Intentionally irreversible: resetting historical snapshots could
        // change commissions for already-created orders and pending payments.
    }
}
