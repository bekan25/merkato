using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Merkato.Infrastructure.Migrations;

[DbContext(typeof(MerkatoDbContext))]
[Migration("20261106040000_AddCreatorCommissionPayoutMetadata")]
public partial class AddCreatorCommissionPayoutMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PayoutReference",
            table: "CreatorCommissions",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PaidAt",
            table: "CreatorCommissions",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PayoutReference", table: "CreatorCommissions");
        migrationBuilder.DropColumn(name: "PaidAt", table: "CreatorCommissions");
    }
}
