using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pwneu.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddForManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ForManager",
                table: "AccessKeys",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForManager",
                table: "AccessKeys");
        }
    }
}
