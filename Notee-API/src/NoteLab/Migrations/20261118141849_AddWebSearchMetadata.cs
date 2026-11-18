using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddWebSearchMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Authority",
                table: "ResearchSources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "ResearchSources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteName",
                table: "ResearchSources",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WebSearches",
                table: "ResearchJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Authority",
                table: "ResearchSources");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "ResearchSources");

            migrationBuilder.DropColumn(
                name: "SiteName",
                table: "ResearchSources");

            migrationBuilder.DropColumn(
                name: "WebSearches",
                table: "ResearchJobs");
        }
    }
}
