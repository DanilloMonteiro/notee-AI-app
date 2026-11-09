using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlainText",
                table: "Notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrashedAt",
                table: "Notes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notes_OwnerId_Status_UpdatedAt",
                table: "Notes",
                columns: new[] { "OwnerId", "Status", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notes_OwnerId_Status_UpdatedAt",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "PlainText",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "TrashedAt",
                table: "Notes");
        }
    }
}
