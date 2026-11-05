using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261105140000_AddBuyerEngagement")]
public sealed class AddBuyerEngagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WishlistItems",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                BuyerId = table.Column<long>(type: "bigint", nullable: false),
                ProductId = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WishlistItems", x => x.Id);
                table.ForeignKey("FK_WishlistItems_Products_ProductId", x => x.ProductId, "Products", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_WishlistItems_Users_BuyerId", x => x.BuyerId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PriceAlerts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                BuyerId = table.Column<long>(type: "bigint", nullable: false),
                ProductId = table.Column<long>(type: "bigint", nullable: false),
                TargetPriceEtb = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                TriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PriceAlerts", x => x.Id);
                table.ForeignKey("FK_PriceAlerts_Products_ProductId", x => x.ProductId, "Products", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_PriceAlerts_Users_BuyerId", x => x.BuyerId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserNotifications",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<long>(type: "bigint", nullable: false),
                Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                ActionUrl = table.Column<string>(type: "text", nullable: true),
                IsRead = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserNotifications", x => x.Id);
                table.ForeignKey("FK_UserNotifications_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_WishlistItems_BuyerId", "WishlistItems", "BuyerId");
        migrationBuilder.CreateIndex("IX_WishlistItems_BuyerId_ProductId", "WishlistItems", new[] { "BuyerId", "ProductId" }, unique: true);
        migrationBuilder.CreateIndex("IX_WishlistItems_ProductId", "WishlistItems", "ProductId");
        migrationBuilder.CreateIndex("IX_PriceAlerts_BuyerId_ProductId_IsActive", "PriceAlerts", new[] { "BuyerId", "ProductId", "IsActive" });
        migrationBuilder.CreateIndex("IX_PriceAlerts_ProductId", "PriceAlerts", "ProductId");
        migrationBuilder.CreateIndex("IX_UserNotifications_UserId_IsRead_CreatedAt", "UserNotifications", new[] { "UserId", "IsRead", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("WishlistItems");
        migrationBuilder.DropTable("PriceAlerts");
        migrationBuilder.DropTable("UserNotifications");
    }
}
