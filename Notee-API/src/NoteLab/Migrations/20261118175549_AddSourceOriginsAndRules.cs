using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceOriginsAndRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceMode",
                table: "UserPreferences",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "mixed");

            migrationBuilder.AddColumn<string>(
                name: "Authors",
                table: "ResearchSources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Doi",
                table: "ResearchSources",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "ResearchSources",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Trusted",
                table: "ResearchSources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AcademicSearches",
                table: "ResearchJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SourceDomainRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceDomainRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceDomainRules_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceDomainRules_OwnerId_Domain",
                table: "SourceDomainRules",
                columns: new[] { "OwnerId", "Domain" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceDomainRules");

            migrationBuilder.DropColumn(
                name: "SourceMode",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "Authors",
                table: "ResearchSources");

            migrationBuilder.DropColumn(
                name: "Doi",
                table: "ResearchSources");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "ResearchSources");

            migrationBuilder.DropColumn(
                name: "Trusted",
                table: "ResearchSources");

            migrationBuilder.DropColumn(
                name: "AcademicSearches",
                table: "ResearchJobs");
        }
    }
}
