using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pwneu.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengeFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Flags",
                table: "Challenges",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Flags",
                table: "Challenges");
        }
    }
}
