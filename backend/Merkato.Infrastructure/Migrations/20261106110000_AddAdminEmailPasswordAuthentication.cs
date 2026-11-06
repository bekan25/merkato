using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261106110000_AddAdminEmailPasswordAuthentication")]
public sealed class AddAdminEmailPasswordAuthentication : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Email",
            table: "Users",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "PasswordHash",
            table: "Users",
            type: "text",
            nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true,
            filter: "\"Email\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Users_Email", table: "Users");
        migrationBuilder.DropColumn(name: "Email", table: "Users");
        migrationBuilder.DropColumn(name: "PasswordHash", table: "Users");
    }
}
