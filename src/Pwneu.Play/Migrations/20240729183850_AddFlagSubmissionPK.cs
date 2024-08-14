using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pwneu.Play.Migrations
{
    /// <inheritdoc />
    public partial class AddFlagSubmissionPK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_FlagSubmissions",
                table: "FlagSubmissions");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "FlagSubmissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_FlagSubmissions",
                table: "FlagSubmissions",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_FlagSubmissions_UserId",
                table: "FlagSubmissions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_FlagSubmissions",
                table: "FlagSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_FlagSubmissions_UserId",
                table: "FlagSubmissions");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "FlagSubmissions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FlagSubmissions",
                table: "FlagSubmissions",
                columns: new[] { "UserId", "ChallengeId" });
        }
    }
}
