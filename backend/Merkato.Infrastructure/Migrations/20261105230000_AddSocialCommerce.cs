using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Merkato.Infrastructure.Migrations;
[DbContext(typeof(MerkatoDbContext))]
[Migration("20261105230000_AddSocialCommerce")]
public partial class AddSocialCommerce:Migration
{
 protected override void Up(MigrationBuilder m){
 m.CreateTable(name:"SocialCommercePosts",columns:t=>new{
 Id=t.Column<long>(type:"bigint").Annotation("Npgsql:ValueGenerationStrategy",Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
 CreatorId=t.Column<long>(type:"bigint"),ProductId=t.Column<long>(type:"bigint"),Caption=t.Column<string>(type:"character varying(1200)",maxLength:1200),
 MediaUrl=t.Column<string>(type:"character varying(2048)",maxLength:2048),MediaType=t.Column<string>(type:"character varying(8)",maxLength:8),Status=t.Column<string>(type:"character varying(16)",maxLength:16),
 ModerationNote=t.Column<string>(type:"character varying(1000)",maxLength:1000,nullable:true),ViewCount=t.Column<int>(type:"integer"),ClickCount=t.Column<int>(type:"integer"),
 CreatedAt=t.Column<DateTime>(type:"timestamp with time zone"),UpdatedAt=t.Column<DateTime>(type:"timestamp with time zone"),PublishedAt=t.Column<DateTime>(type:"timestamp with time zone",nullable:true)
 },constraints:t=>t.PrimaryKey("PK_SocialCommercePosts",x=>x.Id));
 m.CreateTable(name:"SocialCommerceClicks",columns:t=>new{
 Id=t.Column<long>(type:"bigint").Annotation("Npgsql:ValueGenerationStrategy",Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
 PostId=t.Column<long>(type:"bigint"),ViewerId=t.Column<long>(type:"bigint",nullable:true),VisitorHash=t.Column<string>(type:"character varying(64)",maxLength:64),CreatedAt=t.Column<DateTime>(type:"timestamp with time zone")
 },constraints:t=>t.PrimaryKey("PK_SocialCommerceClicks",x=>x.Id));
 m.CreateIndex("IX_SocialCommercePosts_Status_PublishedAt","SocialCommercePosts",new[]{"Status","PublishedAt"});
 m.CreateIndex("IX_SocialCommercePosts_CreatorId_CreatedAt","SocialCommercePosts",new[]{"CreatorId","CreatedAt"});
 m.CreateIndex("IX_SocialCommerceClicks_PostId_VisitorHash","SocialCommerceClicks",new[]{"PostId","VisitorHash"},unique:true);
 }
 protected override void Down(MigrationBuilder m){m.DropTable("SocialCommerceClicks");m.DropTable("SocialCommercePosts");}
}