using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Merkato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPinHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
}

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinHash",
                table: "Users");
}
    }
}

