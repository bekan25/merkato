using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Merkato.Infrastructure.Migrations;
[DbContext(typeof(MerkatoDbContext))]
[Migration("20261105220000_AddMultiMerchantSourcing")]
public partial class AddMultiMerchantSourcing : Migration
{
 protected override void Up(MigrationBuilder m)
 {
  m.CreateTable(name:"SourcingRequests", columns:t=>new {
   Id=t.Column<long>(type:"bigint").Annotation("Npgsql:ValueGenerationStrategy",Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
   RequestNumber=t.Column<string>(type:"character varying(24)",maxLength:24), BuyerId=t.Column<long>(type:"bigint"),
   Title=t.Column<string>(type:"character varying(160)",maxLength:160), Description=t.Column<string>(type:"character varying(2000)",maxLength:2000),
   Category=t.Column<string>(type:"character varying(100)",maxLength:100), City=t.Column<string>(type:"character varying(100)",maxLength:100),
   Quantity=t.Column<int>(type:"integer"), TargetUnitPriceEtb=t.Column<decimal>(type:"numeric(18,4)",nullable:true),
   Status=t.Column<string>(type:"character varying(24)",maxLength:24), ExpiresAt=t.Column<DateTime>(type:"timestamp with time zone"),
   CreatedAt=t.Column<DateTime>(type:"timestamp with time zone"), UpdatedAt=t.Column<DateTime>(type:"timestamp with time zone")
  }, constraints:t=>t.PrimaryKey("PK_SourcingRequests",x=>x.Id));
  m.CreateTable(name:"SourcingOffers", columns:t=>new {
   Id=t.Column<long>(type:"bigint").Annotation("Npgsql:ValueGenerationStrategy",Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
   SourcingRequestId=t.Column<long>(type:"bigint"), SellerId=t.Column<long>(type:"bigint"), UnitPriceEtb=t.Column<decimal>(type:"numeric(18,4)"),
   AvailableQuantity=t.Column<int>(type:"integer"), Note=t.Column<string>(type:"character varying(1000)",maxLength:1000,nullable:true),
   LeadTimeDays=t.Column<int>(type:"integer",nullable:true), Status=t.Column<string>(type:"character varying(24)",maxLength:24),
   CreatedAt=t.Column<DateTime>(type:"timestamp with time zone"), UpdatedAt=t.Column<DateTime>(type:"timestamp with time zone")
  }, constraints:t=>t.PrimaryKey("PK_SourcingOffers",x=>x.Id));
  m.CreateIndex("IX_SourcingRequests_RequestNumber","SourcingRequests","RequestNumber",unique:true);
  m.CreateIndex("IX_SourcingRequests_BuyerId_CreatedAt","SourcingRequests",new[]{"BuyerId","CreatedAt"});
  m.CreateIndex("IX_SourcingRequests_Status_ExpiresAt","SourcingRequests",new[]{"Status","ExpiresAt"});
  m.CreateIndex("IX_SourcingOffers_SourcingRequestId_SellerId","SourcingOffers",new[]{"SourcingRequestId","SellerId"},unique:true);
  m.CreateIndex("IX_SourcingOffers_SellerId_Status","SourcingOffers",new[]{"SellerId","Status"});
 }
 protected override void Down(MigrationBuilder m)
 {
  m.DropTable("SourcingOffers");
  m.DropTable("SourcingRequests");
 }
}