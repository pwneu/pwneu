using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Pwneu.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "dfaac79a-82b3-44c4-8b5d-5a4065ca3e7e", null, "User", "USER" },
                    { "f666afc3-6656-4141-b147-c8f98b533dc7", null, "Admin", "ADMIN" },
                    { "fe139335-c42c-416b-a721-1262de9e3af7", null, "Faculty", "FACULTY" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "dfaac79a-82b3-44c4-8b5d-5a4065ca3e7e");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "f666afc3-6656-4141-b147-c8f98b533dc7");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "fe139335-c42c-416b-a721-1262de9e3af7");
        }
    }
}
