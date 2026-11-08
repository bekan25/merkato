using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261105160000_AddReferralRewardTargeting")]
public sealed class AddReferralRewardTargeting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(name: "AssignedUserId", table: "PromotionCoupons", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "SourceReferralId", table: "PromotionCoupons", type: "bigint", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_PromotionCoupons_SourceReferralId", table: "PromotionCoupons", column: "SourceReferralId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_PromotionCoupons_SourceReferralId", table: "PromotionCoupons");
        migrationBuilder.DropColumn(name: "AssignedUserId", table: "PromotionCoupons");
        migrationBuilder.DropColumn(name: "SourceReferralId", table: "PromotionCoupons");
    }
}
