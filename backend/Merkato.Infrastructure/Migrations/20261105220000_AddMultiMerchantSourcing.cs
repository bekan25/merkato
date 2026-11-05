using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Merkato.Infrastructure.Migrations;
[DbContext(typeof(MerkatoDbContext))]
[Migration("20261105220000_AddMultiMerchantSourcing")]
public partial class AddMultiMerchantSourcing : Migration
{
 protected override void Up(MigrationBuilder m){
  m.CreateTable("SourcingRequests",t=>new{id=t.Column<long>("bigint").Annotation("Npgsql:ValueGenerationStrategy",Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),RequestNumber=t.Column<string>("character varying(24)",maxLength:24),BuyerId=t.Column<long>("bigint"),Title=t.Column<string>("character varying(160)",maxLength:160),Description=t.Column<string>("character varying(2000)",maxLength:2000),Category=t.Column<string>("character varying(100)",maxLength:100),City=t.Column<string>("character varying(100)",maxLength:100),Quantity=t.Column<int>("integer"),TargetUnitPriceEtb=t.Column<decimal>("numeric(18,4)",nullable:true),Status=t.Column<string>("character varying(24)",maxLength:24),ExpiresAt=t.Column<DateTime>("timestamp with time zone"),CreatedAt=t.Column<DateTime>("timestamp with time zone"),UpdatedAt=t.Column<DateTime>("timestamp with time zone")},c=>c.PrimaryKey("PK_SourcingRequests",x=>x.id));
  m.CreateTable("SourcingOffers",t=>new{id=t.Column<long>("bigint").Annotation("Npgsql:ValueGenerationStrategy",Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),SourcingRequestId=t.Column<long>("bigint"),SellerId=t.Column<long>("bigint"),UnitPriceEtb=t.Column<decimal>("numeric(18,4)"),AvailableQuantity=t.Column<int>("integer"),Note=t.Column<string>("character varying(1000)",maxLength:1000,nullable:true),LeadTimeDays=t.Column<int>("integer",nullable:true),Status=t.Column<string>("character varying(24)",maxLength:24),CreatedAt=t.Column<DateTime>("timestamp with time zone"),UpdatedAt=t.Column<DateTime>("timestamp with time zone")},c=>c.PrimaryKey("PK_SourcingOffers",x=>x.id));
  m.CreateIndex("IX_SourcingRequests_RequestNumber","SourcingRequests","RequestNumber",unique:true);m.CreateIndex("IX_SourcingRequests_BuyerId_CreatedAt","SourcingRequests",new[]{"BuyerId","CreatedAt"});m.CreateIndex("IX_SourcingRequests_Status_ExpiresAt","SourcingRequests",new[]{"Status","ExpiresAt"});m.CreateIndex("IX_SourcingOffers_SourcingRequestId_SellerId","SourcingOffers",new[]{"SourcingRequestId","SellerId"},unique:true);m.CreateIndex("IX_SourcingOffers_SellerId_Status","SourcingOffers",new[]{"SellerId","Status"});
 }
 protected override void Down(MigrationBuilder m){m.DropTable("SourcingOffers");m.DropTable("SourcingRequests");}
}