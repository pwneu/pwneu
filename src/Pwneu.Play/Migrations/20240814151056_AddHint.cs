using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pwneu.Play.Migrations
{
    /// <inheritdoc />
    public partial class AddHint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Deduction = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hints_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HintUsages",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    HintId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HintUsages", x => new { x.UserId, x.HintId });
                    table.ForeignKey(
                        name: "FK_HintUsages_Hints_HintId",
                        column: x => x.HintId,
                        principalTable: "Hints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hints_ChallengeId",
                table: "Hints",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_HintUsages_HintId",
                table: "HintUsages",
                column: "HintId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HintUsages");

            migrationBuilder.DropTable(
                name: "Hints");
        }
    }
}
