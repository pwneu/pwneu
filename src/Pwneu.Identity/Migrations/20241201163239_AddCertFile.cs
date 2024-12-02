using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pwneu.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddCertFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssuedAt",
                table: "Certificates");

            migrationBuilder.RenameColumn(
                name: "Issuer",
                table: "Certificates",
                newName: "FileName");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "Certificates",
                newName: "ContentType");

            migrationBuilder.AddColumn<byte[]>(
                name: "Data",
                table: "Certificates",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_UserId",
                table: "Certificates",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Certificates_AspNetUsers_UserId",
                table: "Certificates",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Certificates_AspNetUsers_UserId",
                table: "Certificates");

            migrationBuilder.DropIndex(
                name: "IX_Certificates_UserId",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "Data",
                table: "Certificates");

            migrationBuilder.RenameColumn(
                name: "FileName",
                table: "Certificates",
                newName: "Issuer");

            migrationBuilder.RenameColumn(
                name: "ContentType",
                table: "Certificates",
                newName: "FullName");

            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedAt",
                table: "Certificates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);
        }
    }
}
