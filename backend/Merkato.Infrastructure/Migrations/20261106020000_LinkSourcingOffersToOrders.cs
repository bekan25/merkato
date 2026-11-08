using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Merkato.Infrastructure.Migrations;
[DbContext(typeof(MerkatoDbContext))]
[Migration("20261106020000_LinkSourcingOffersToOrders")]
public sealed class LinkSourcingOffersToOrders : Migration
{
 protected override void Up(MigrationBuilder m)
 {
  m.AddColumn<long>(name:"ProductId",table:"SourcingOffers",type:"bigint",nullable:true);
  m.AddColumn<long>(name:"SourcingRequestId",table:"Orders",type:"bigint",nullable:true);
  m.AddColumn<long>(name:"SourcingOfferId",table:"Orders",type:"bigint",nullable:true);
  m.CreateIndex(name:"IX_SourcingOffers_ProductId",table:"SourcingOffers",column:"ProductId");
  m.CreateIndex(name:"IX_Orders_SourcingRequestId",table:"Orders",column:"SourcingRequestId",unique:true);
 }
 protected override void Down(MigrationBuilder m)
 {
  m.DropIndex(name:"IX_SourcingOffers_ProductId",table:"SourcingOffers");
  m.DropIndex(name:"IX_Orders_SourcingRequestId",table:"Orders");
  m.DropColumn(name:"ProductId",table:"SourcingOffers");
  m.DropColumn(name:"SourcingRequestId",table:"Orders");
  m.DropColumn(name:"SourcingOfferId",table:"Orders");
 }
}