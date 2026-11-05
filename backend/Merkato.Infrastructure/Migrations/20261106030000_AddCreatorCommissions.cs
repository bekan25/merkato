using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Merkato.Infrastructure.Migrations;
[DbContext(typeof(MerkatoDbContext))]
[Migration("20261106030000_AddCreatorCommissions")]
public partial class AddCreatorCommissions : Migration
{
 protected override void Up(MigrationBuilder m)
 {
  m.CreateTable(name:"CreatorCommissions",columns:t=>new {
   Id=t.Column<long>(type:"bigint").Annotation("Npgsql:ValueGenerationStrategy",Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
   CreatorId=t.Column<long>(type:"bigint"),OrderId=t.Column<long>(type:"bigint"),PostId=t.Column<long>(type:"bigint"),
   EligibleAmountEtb=t.Column<decimal>(type:"numeric(18,4)"),RatePercent=t.Column<decimal>(type:"numeric(7,4)"),
   CommissionEtb=t.Column<decimal>(type:"numeric(18,4)"),Status=t.Column<string>(type:"character varying(32)",maxLength:32),
   CreatedAt=t.Column<DateTime>(type:"timestamp with time zone"),ReleasedAt=t.Column<DateTime>(type:"timestamp with time zone",nullable:true)
  },constraints:t=>t.PrimaryKey("PK_CreatorCommissions",x=>x.Id));
  m.CreateIndex("IX_CreatorCommissions_OrderId","CreatorCommissions","OrderId",unique:true);
  m.CreateIndex("IX_CreatorCommissions_CreatorId_CreatedAt","CreatorCommissions",new[]{"CreatorId","CreatedAt"});
 }
 protected override void Down(MigrationBuilder m)=>m.DropTable("CreatorCommissions");
}