using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable
namespace Merkato.Infrastructure.Migrations;
[DbContext(typeof(MerkatoDbContext))]
[Migration("20261107100000_AddUserAccountProfileAndAddresses")]
public partial class AddUserAccountProfileAndAddresses : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  migrationBuilder.AddColumn<byte[]>(name: "ProfilePhotoContent", table: "Users", type: "bytea", nullable: true);
  migrationBuilder.AddColumn<string>(name: "ProfilePhotoContentType", table: "Users", type: "character varying(80)", maxLength: 80, nullable: true);
  migrationBuilder.CreateTable(name: "UserAddresses",
   columns: table => new
   {
    Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
    UserId = table.Column<long>(type: "bigint", nullable: false),
    Label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
    RecipientName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
    SubCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
    Woreda = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
    StreetAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
    DeliveryInstructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
   },
   constraints: table =>
   {
    table.PrimaryKey("PK_UserAddresses", x => x.Id);
    table.ForeignKey("FK_UserAddresses_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
   });
  migrationBuilder.CreateIndex(name: "IX_UserAddresses_UserId", table: "UserAddresses", column: "UserId", unique: true, filter: "\"IsDefault\" = TRUE");
 }
 protected override void Down(MigrationBuilder migrationBuilder)
 {
  migrationBuilder.DropTable("UserAddresses");
  migrationBuilder.DropColumn(name: "ProfilePhotoContent", table: "Users");
  migrationBuilder.DropColumn(name: "ProfilePhotoContentType", table: "Users");
 }
}