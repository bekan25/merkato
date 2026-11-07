using System;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Merkato.Infrastructure.Migrations;
public partial class AddUserNotificationPreferences : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  migrationBuilder.CreateTable(name: "UserNotificationPreferences",
   columns: table => new
   {
    UserId = table.Column<long>(type: "bigint", nullable: false),
    LanguageCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
    InAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
    SmsEnabled = table.Column<bool>(type: "boolean", nullable: false),
    MarketingEnabled = table.Column<bool>(type: "boolean", nullable: false),
    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
   },
   constraints: table =>
   {
    table.PrimaryKey("PK_UserNotificationPreferences", x => x.UserId);
    table.ForeignKey("FK_UserNotificationPreferences_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
   });
 }
 protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("UserNotificationPreferences");
}