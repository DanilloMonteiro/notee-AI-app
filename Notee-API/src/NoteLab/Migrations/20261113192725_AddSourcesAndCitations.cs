using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcesAndCitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CitationsJson",
                table: "PublicationRevisions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Authors = table.Column<string>(type: "jsonb", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Venue = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Doi = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Isbn = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    License = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceCitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExactQuote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartIndex = table.Column<int>(type: "integer", nullable: true),
                    EndIndex = table.Column<int>(type: "integer", nullable: true),
                    LocatorKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LocatorText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PageStart = table.Column<int>(type: "integer", nullable: true),
                    PageEnd = table.Column<int>(type: "integer", nullable: true),
                    TimeStartSec = table.Column<int>(type: "integer", nullable: true),
                    TimeEndSec = table.Column<int>(type: "integer", nullable: true),
                    SourceQuote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceCitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceCitations_Notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SourceCitations_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceCitations_NoteId_OwnerId",
                table: "SourceCitations",
                columns: new[] { "NoteId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceCitations_SourceId_OwnerId",
                table: "SourceCitations",
                columns: new[] { "SourceId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sources_OwnerId_Doi",
                table: "Sources",
                columns: new[] { "OwnerId", "Doi" },
                unique: true,
                filter: "\"Doi\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_OwnerId_Isbn",
                table: "Sources",
                columns: new[] { "OwnerId", "Isbn" },
                unique: true,
                filter: "\"Isbn\" IS NOT NULL");

            migrationBuilder.Sql("""
                INSERT INTO "Sources" (
                    "Id", "OwnerId", "Kind", "Title", "Authors", "Year", "Venue", "Language",
                    "Doi", "Isbn", "Url", "License", "Notes", "CreatedAt", "UpdatedAt")
                SELECT r."Id", r."OwnerId",
                    CASE WHEN r."Kind" = 'Book' THEN 'Book' WHEN r."Url" IS NOT NULL THEN 'Website' ELSE 'Document' END,
                    r."Title",
                    CASE WHEN r."Kind" = 'Book' AND r."Author" IS NOT NULL THEN jsonb_build_array(r."Author") ELSE '[]'::jsonb END,
                    CASE WHEN r."Kind" = 'Book' AND r."PublicationYear" ~ '^[1-9][0-9]{3}$'
                        THEN r."PublicationYear"::integer ELSE NULL END,
                    CASE WHEN r."Kind" = 'Source' THEN r."Publisher" ELSE NULL END,
                    NULL, NULL, NULL, r."Url", 'unknown', NULL, r."CreatedAt", r."CreatedAt"
                FROM "NoteReferences" r;

                INSERT INTO "SourceCitations" (
                    "Id", "OwnerId", "NoteId", "SourceId", "BlockId", "ExactQuote", "StartIndex", "EndIndex",
                    "LocatorKind", "LocatorText", "PageStart", "PageEnd", "TimeStartSec", "TimeEndSec", "SourceQuote", "CreatedAt")
                SELECT r."Id", r."OwnerId", r."NoteId", r."Id", NULL, NULL, NULL, NULL,
                    'None', NULL, NULL, NULL, NULL, NULL, NULL, r."CreatedAt"
                FROM "NoteReferences" r;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceCitations");

            migrationBuilder.DropTable(
                name: "Sources");

            migrationBuilder.DropColumn(
                name: "CitationsJson",
                table: "PublicationRevisions");
        }
    }
}
