using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261117090000_AddProductLanguageSearch")]
public sealed class AddProductLanguageSearch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Products",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LanguageCode",
            table: "Products",
            type: "character varying(5)",
            maxLength: 5,
            nullable: false,
            defaultValue: "en");

        migrationBuilder.CreateIndex(
            name: "IX_Products_LanguageCode",
            table: "Products",
            column: "LanguageCode");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Products_LanguageCode",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "LanguageCode",
            table: "Products");
    }
}
