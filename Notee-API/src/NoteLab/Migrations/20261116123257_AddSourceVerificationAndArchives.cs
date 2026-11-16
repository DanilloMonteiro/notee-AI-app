using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceVerificationAndArchives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    TextKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Basis = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OriginUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SnapshotUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceArchives_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Layer = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ArgumentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Verdict = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    CostUsd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceChecks_SourceCitations_CitationId",
                        column: x => x.CitationId,
                        principalTable: "SourceCitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SourceChecks_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceArchives_OwnerId_SizeBytes",
                table: "SourceArchives",
                columns: new[] { "OwnerId", "SizeBytes" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceArchives_OwnerId_SourceId_CreatedAt",
                table: "SourceArchives",
                columns: new[] { "OwnerId", "SourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceArchives_SourceId",
                table: "SourceArchives",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceChecks_CitationId",
                table: "SourceChecks",
                column: "CitationId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceChecks_OwnerId_CreatedAt",
                table: "SourceChecks",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceChecks_SourceId_Layer_CreatedAt",
                table: "SourceChecks",
                columns: new[] { "SourceId", "Layer", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceChecks_Status_CreatedAt",
                table: "SourceChecks",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceArchives");

            migrationBuilder.DropTable(
                name: "SourceChecks");
        }
    }
}
