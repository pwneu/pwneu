using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pwneu.Play.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengeSolveCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SolveCount",
                table: "Challenges",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SolveCount",
                table: "Challenges");
        }
    }
}
