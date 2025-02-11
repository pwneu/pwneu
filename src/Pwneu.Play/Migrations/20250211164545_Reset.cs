using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pwneu.Play.Migrations
{
    /// <inheritdoc />
    public partial class Reset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "play");

            migrationBuilder.CreateTable(
                name: "Audits",
                schema: "play",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "play",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayConfigurations",
                schema: "play",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayConfigurations", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Challenges",
                schema: "play",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    DeadlineEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    SolveCount = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    Flags = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Challenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Challenges_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "play",
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Artifacts",
                schema: "play",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artifacts_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalSchema: "play",
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hints",
                schema: "play",
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
                        principalSchema: "play",
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Solves",
                schema: "play",
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
                        principalSchema: "play",
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                schema: "play",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Flag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Submissions_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalSchema: "play",
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HintUsages",
                schema: "play",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    HintId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HintUsages", x => new { x.UserId, x.HintId });
                    table.ForeignKey(
                        name: "FK_HintUsages_Hints_HintId",
                        column: x => x.HintId,
                        principalSchema: "play",
                        principalTable: "Hints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_ChallengeId",
                schema: "play",
                table: "Artifacts",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_CategoryId",
                schema: "play",
                table: "Challenges",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Hints_ChallengeId",
                schema: "play",
                table: "Hints",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_HintUsages_HintId",
                schema: "play",
                table: "HintUsages",
                column: "HintId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayConfigurations_Key",
                schema: "play",
                table: "PlayConfigurations",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Solves_ChallengeId",
                schema: "play",
                table: "Solves",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ChallengeId",
                schema: "play",
                table: "Submissions",
                column: "ChallengeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Artifacts",
                schema: "play");

            migrationBuilder.DropTable(
                name: "Audits",
                schema: "play");

            migrationBuilder.DropTable(
                name: "HintUsages",
                schema: "play");

            migrationBuilder.DropTable(
                name: "PlayConfigurations",
                schema: "play");

            migrationBuilder.DropTable(
                name: "Solves",
                schema: "play");

            migrationBuilder.DropTable(
                name: "Submissions",
                schema: "play");

            migrationBuilder.DropTable(
                name: "Hints",
                schema: "play");

            migrationBuilder.DropTable(
                name: "Challenges",
                schema: "play");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "play");
        }
    }
}
