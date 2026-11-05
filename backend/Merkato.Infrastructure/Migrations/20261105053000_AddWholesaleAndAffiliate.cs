using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Merkato.Infrastructure.Persistence;

#nullable disable

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]\n[Migration("20261105053000_AddWholesaleAndAffiliate")]\npublic partial class AddWholesaleAndAffiliate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WholesalePriceTiers",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ProductId = table.Column<long>(type: "bigint", nullable: false),
                SellerId = table.Column<long>(type: "bigint", nullable: false),
                MinimumQuantity = table.Column<int>(type: "integer", nullable: false),
                UnitPriceEtb = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WholesalePriceTiers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "WholesaleRequests",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RequestNumber = table.Column<string>(type: "text", nullable: false),
                ProductId = table.Column<long>(type: "bigint", nullable: false),
                BuyerId = table.Column<long>(type: "bigint", nullable: false),
                SellerId = table.Column<long>(type: "bigint", nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                RequestedUnitPriceEtb = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                QuotedUnitPriceEtb = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                Status = table.Column<string>(type: "text", nullable: false),
                BuyerNote = table.Column<string>(type: "text", nullable: true),
                SellerNote = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WholesaleRequests", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AffiliateProfiles",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<long>(type: "bigint", nullable: false),
                ReferralCode = table.Column<string>(type: "text", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AffiliateProfiles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AffiliateReferrals",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AffiliateUserId = table.Column<long>(type: "bigint", nullable: false),
                ReferralCode = table.Column<string>(type: "text", nullable: false),
                VisitorKeyHash = table.Column<string>(type: "text", nullable: false),
                ReferredUserId = table.Column<long>(type: "bigint", nullable: true),
                ClickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AffiliateReferrals", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AffiliateCommissions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AffiliateUserId = table.Column<long>(type: "bigint", nullable: false),
                OrderId = table.Column<long>(type: "bigint", nullable: false),
                EligibleAmountEtb = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                RatePercent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                CommissionEtb = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                PayoutReference = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AffiliateCommissions", x => x.Id));

        migrationBuilder.CreateIndex("IX_WholesalePriceTiers_ProductId_MinimumQuantity", "WholesalePriceTiers", new[] { "ProductId", "MinimumQuantity" }, unique: true);
        migrationBuilder.CreateIndex("IX_WholesaleRequests_RequestNumber", "WholesaleRequests", "RequestNumber", unique: true);
        migrationBuilder.CreateIndex("IX_WholesaleRequests_SellerId_Status", "WholesaleRequests", new[] { "SellerId", "Status" });
        migrationBuilder.CreateIndex("IX_AffiliateProfiles_UserId", "AffiliateProfiles", "UserId", unique: true);
        migrationBuilder.CreateIndex("IX_AffiliateProfiles_ReferralCode", "AffiliateProfiles", "ReferralCode", unique: true);
        migrationBuilder.CreateIndex("IX_AffiliateReferrals_AffiliateUserId_VisitorKeyHash", "AffiliateReferrals", new[] { "AffiliateUserId", "VisitorKeyHash" }, unique: true);
        migrationBuilder.CreateIndex("IX_AffiliateReferrals_ReferredUserId", "AffiliateReferrals", "ReferredUserId", unique: true);
        migrationBuilder.CreateIndex("IX_AffiliateCommissions_OrderId", "AffiliateCommissions", "OrderId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("AffiliateCommissions");
        migrationBuilder.DropTable("AffiliateProfiles");
        migrationBuilder.DropTable("AffiliateReferrals");
        migrationBuilder.DropTable("WholesalePriceTiers");
        migrationBuilder.DropTable("WholesaleRequests");
    }
}