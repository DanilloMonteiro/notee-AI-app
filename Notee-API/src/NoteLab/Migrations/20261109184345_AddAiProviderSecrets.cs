using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddAiProviderSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiProviderSecrets",
                columns: table => new
                {
                    ProviderId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProtectedKey = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderSecrets", x => x.ProviderId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiProviderSecrets");
        }
    }
}
