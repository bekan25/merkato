using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Merkato.Infrastructure.Persistence;

#nullable disable

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261105120000_AddMarketplaceGrowthFeatures")]
public sealed class AddMarketplaceGrowthFeatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "ProductListingDetails" (
                "ProductId" bigint PRIMARY KEY REFERENCES "Products"("Id") ON DELETE CASCADE,
                "Condition" integer NOT NULL DEFAULT 0,
                "StockQuantity" integer NOT NULL DEFAULT 0 CHECK ("StockQuantity" >= 0),
                "LowStockThreshold" integer NOT NULL DEFAULT 5 CHECK ("LowStockThreshold" >= 0),
                "UpdatedAt" timestamptz NOT NULL DEFAULT now()
            );
            CREATE TABLE IF NOT EXISTS "ProductReviews" (
                "Id" bigserial PRIMARY KEY,
                "ProductId" bigint NOT NULL REFERENCES "Products"("Id") ON DELETE CASCADE,
                "BuyerId" bigint NOT NULL REFERENCES "Users"("Id") ON DELETE RESTRICT,
                "OrderId" bigint NOT NULL REFERENCES "Orders"("Id") ON DELETE RESTRICT,
                "Rating" integer NOT NULL CHECK ("Rating" BETWEEN 1 AND 5),
                "Comment" text NOT NULL DEFAULT '',
                "CreatedAt" timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT "UX_ProductReviews_ProductId_BuyerId" UNIQUE ("ProductId","BuyerId")
            );
            CREATE INDEX IF NOT EXISTS "IX_ProductReviews_OrderId" ON "ProductReviews"("OrderId");
            CREATE TABLE IF NOT EXISTS "ProductPriceSnapshots" (
                "Id" bigserial PRIMARY KEY,
                "ProductId" bigint NOT NULL REFERENCES "Products"("Id") ON DELETE CASCADE,
                "SellerId" bigint NOT NULL REFERENCES "Users"("Id") ON DELETE RESTRICT,
                "PriceEtb" numeric(18,4) NOT NULL CHECK ("PriceEtb" > 0),
                "RecordedAt" timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS "IX_ProductPriceSnapshots_ProductId_RecordedAt" ON "ProductPriceSnapshots"("ProductId","RecordedAt");
            CREATE TABLE IF NOT EXISTS "GroupBuys" (
                "Id" bigserial PRIMARY KEY,
                "ProductId" bigint NOT NULL REFERENCES "Products"("Id") ON DELETE RESTRICT,
                "OrganizerId" bigint NOT NULL REFERENCES "Users"("Id") ON DELETE RESTRICT,
                "TargetQuantity" integer NOT NULL CHECK ("TargetQuantity" > 1),
                "JoinedQuantity" integer NOT NULL DEFAULT 0 CHECK ("JoinedQuantity" >= 0),
                "RegularUnitPriceEtb" numeric(18,4) NOT NULL CHECK ("RegularUnitPriceEtb" > 0),
                "DiscountUnitPriceEtb" numeric(18,4) NOT NULL CHECK ("DiscountUnitPriceEtb" > 0 AND "DiscountUnitPriceEtb" <= "RegularUnitPriceEtb"),
                "ExpiresAt" timestamptz NOT NULL,
                "Status" integer NOT NULL DEFAULT 0,
                "CreatedAt" timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS "IX_GroupBuys_Status_ExpiresAt" ON "GroupBuys"("Status","ExpiresAt");
            CREATE TABLE IF NOT EXISTS "GroupBuyMembers" (
                "Id" bigserial PRIMARY KEY,
                "GroupBuyId" bigint NOT NULL REFERENCES "GroupBuys"("Id") ON DELETE CASCADE,
                "BuyerId" bigint NOT NULL REFERENCES "Users"("Id") ON DELETE RESTRICT,
                "Quantity" integer NOT NULL CHECK ("Quantity" > 0),
                "JoinedAt" timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT "UX_GroupBuyMembers_GroupBuyId_BuyerId" UNIQUE ("GroupBuyId","BuyerId")
            );
            CREATE TABLE IF NOT EXISTS "ReturnRequests" (
                "Id" bigserial PRIMARY KEY,
                "OrderId" bigint NOT NULL REFERENCES "Orders"("Id") ON DELETE RESTRICT,
                "BuyerId" bigint NOT NULL REFERENCES "Users"("Id") ON DELETE RESTRICT,
                "SellerId" bigint NOT NULL REFERENCES "Users"("Id") ON DELETE RESTRICT,
                "Reason" text NOT NULL,
                "EvidenceUrl" text NULL,
                "ResolutionNote" text NULL,
                "Status" integer NOT NULL DEFAULT 0,
                "CreatedAt" timestamptz NOT NULL DEFAULT now(),
                "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT "UX_ReturnRequests_OrderId" UNIQUE ("OrderId")
            );
            CREATE TABLE IF NOT EXISTS "LoyaltyAccounts" (
                "UserId" bigint PRIMARY KEY REFERENCES "Users"("Id") ON DELETE CASCADE,
                "PointsBalance" integer NOT NULL DEFAULT 0 CHECK ("PointsBalance" >= 0),
                "UpdatedAt" timestamptz NOT NULL DEFAULT now()
            );
            CREATE TABLE IF NOT EXISTS "LoyaltyTransactions" (
                "Id" bigserial PRIMARY KEY,
                "UserId" bigint NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
                "Points" integer NOT NULL,
                "Reason" varchar(250) NOT NULL,
                "OrderId" bigint NULL REFERENCES "Orders"("Id") ON DELETE SET NULL,
                "CreatedAt" timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS "IX_LoyaltyTransactions_UserId_CreatedAt" ON "LoyaltyTransactions"("UserId","CreatedAt" DESC);

            CREATE OR REPLACE FUNCTION merkato_record_product_price() RETURNS trigger AS $$
            BEGIN
                IF TG_OP = 'INSERT' OR NEW."PriceEtb" IS DISTINCT FROM OLD."PriceEtb" THEN
                    INSERT INTO "ProductPriceSnapshots" ("ProductId","SellerId","PriceEtb","RecordedAt")
                    VALUES (NEW."Id", NEW."SellerId", NEW."PriceEtb", now());
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            DROP TRIGGER IF EXISTS trg_merkato_product_price_history ON "Products";
            CREATE TRIGGER trg_merkato_product_price_history
            AFTER INSERT OR UPDATE OF "PriceEtb" ON "Products"
            FOR EACH ROW EXECUTE FUNCTION merkato_record_product_price();
            INSERT INTO "ProductPriceSnapshots" ("ProductId","SellerId","PriceEtb","RecordedAt")
            SELECT p."Id",p."SellerId",p."PriceEtb",p."CreatedAt" FROM "Products" p
            WHERE NOT EXISTS (SELECT 1 FROM "ProductPriceSnapshots" s WHERE s."ProductId"=p."Id");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_merkato_product_price_history ON "Products";
            DROP FUNCTION IF EXISTS merkato_record_product_price();
            DROP TABLE IF EXISTS "LoyaltyTransactions";
            DROP TABLE IF EXISTS "LoyaltyAccounts";
            DROP TABLE IF EXISTS "ReturnRequests";
            DROP TABLE IF EXISTS "GroupBuyMembers";
            DROP TABLE IF EXISTS "GroupBuys";
            DROP TABLE IF EXISTS "ProductPriceSnapshots";
            DROP TABLE IF EXISTS "ProductReviews";
            DROP TABLE IF EXISTS "ProductListingDetails";
            """);
    }
}
