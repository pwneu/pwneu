using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pwneu.Play.Migrations
{
    /// <inheritdoc />
    public partial class SplitSubmissionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCorrect",
                table: "Submissions");

            migrationBuilder.CreateTable(
                name: "Solves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Flag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Solves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Solves_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Solves_ChallengeId",
                table: "Solves",
                column: "ChallengeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Solves");

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrect",
                table: "Submissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
