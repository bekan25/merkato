using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261105150000_AddPromotionCoupons")]
public sealed class AddPromotionCoupons : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PromotionCoupons",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                DiscountType = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                DiscountValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                MaximumDiscountEtb = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                MinimumOrderEtb = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                SellerId = table.Column<long>(type: "bigint", nullable: true),
                UsageLimit = table.Column<int>(type: "integer", nullable: true),
                PerUserLimit = table.Column<int>(type: "integer", nullable: false),
                StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PromotionCoupons", x => x.Id));

        migrationBuilder.CreateTable(
            name: "CouponRedemptions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CouponId = table.Column<long>(type: "bigint", nullable: false),
                BuyerId = table.Column<long>(type: "bigint", nullable: false),
                OrderId = table.Column<long>(type: "bigint", nullable: false),
                DiscountEtb = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CouponRedemptions", x => x.Id);
                table.ForeignKey("FK_CouponRedemptions_PromotionCoupons_CouponId", x => x.CouponId, "PromotionCoupons", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CouponRedemptions_Users_BuyerId", x => x.BuyerId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CouponRedemptions_Orders_OrderId", x => x.OrderId, "Orders", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_PromotionCoupons_Code", "PromotionCoupons", "Code", unique: true);
        migrationBuilder.CreateIndex("IX_PromotionCoupons_IsActive_StartsAt_ExpiresAt", "PromotionCoupons", new[] { "IsActive", "StartsAt", "ExpiresAt" });
        migrationBuilder.CreateIndex("IX_CouponRedemptions_CouponId_BuyerId", "CouponRedemptions", new[] { "CouponId", "BuyerId" });
        migrationBuilder.CreateIndex("IX_CouponRedemptions_BuyerId", "CouponRedemptions", "BuyerId");
        migrationBuilder.CreateIndex("IX_CouponRedemptions_OrderId", "CouponRedemptions", "OrderId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("CouponRedemptions");
        migrationBuilder.DropTable("PromotionCoupons");
    }
}
