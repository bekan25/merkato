using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261108120000_AddAffiliateReferralCommissionUniqueness")]
public sealed class AddAffiliateReferralCommissionUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "AffiliateReferralId",
            table: "AffiliateCommissions",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AffiliateCommissions_AffiliateReferralId",
            table: "AffiliateCommissions",
            column: "AffiliateReferralId",
            unique: true,
            filter: "\"AffiliateReferralId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AffiliateCommissions_AffiliateReferralId",
            table: "AffiliateCommissions");

        migrationBuilder.DropColumn(
            name: "AffiliateReferralId",
            table: "AffiliateCommissions");
    }
}
