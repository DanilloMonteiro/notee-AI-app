using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddResearchJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "AnchorStartIndex",
                table: "Doubts",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "AnchorExactQuote",
                table: "Doubts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "AnchorEndIndex",
                table: "Doubts",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "AnchorBlockId",
                table: "Doubts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "AnswerProvider",
                table: "Doubts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Evidence",
                table: "Doubts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Doubts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Selection");

            migrationBuilder.CreateTable(
                name: "ResearchJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DoubtId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FencingToken = table.Column<long>(type: "bigint", nullable: false),
                    CancelRequested = table.Column<bool>(type: "boolean", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LastError = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResearchJobs_Doubts_DoubtId",
                        column: x => x.DoubtId,
                        principalTable: "Doubts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Doubts_OwnerId_NoteId",
                table: "Doubts",
                columns: new[] { "OwnerId", "NoteId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchJobs_ActivePerDoubt",
                table: "ResearchJobs",
                column: "DoubtId",
                unique: true,
                filter: "\"Status\" IN ('Queued', 'Running')");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchJobs_OwnerId_IdempotencyKey",
                table: "ResearchJobs",
                columns: new[] { "OwnerId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchJobs_Status_NextAttemptAt",
                table: "ResearchJobs",
                columns: new[] { "Status", "NextAttemptAt" });

            // Dúvidas pendentes/em andamento de antes dos jobs: enfileira para o novo worker.
            migrationBuilder.Sql("""
                UPDATE "Doubts" SET "Status" = 0 WHERE "Status" = 1;
                INSERT INTO "ResearchJobs" ("Id", "DoubtId", "OwnerId", "Status", "Attempt", "MaxAttempts", "NextAttemptAt",
                    "FencingToken", "CancelRequested", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), d."Id", d."OwnerId", 'Queued', 0, 3, now(), 0, false, now(), now()
                FROM "Doubts" d WHERE d."Status" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResearchJobs");

            migrationBuilder.DropIndex(
                name: "IX_Doubts_OwnerId_NoteId",
                table: "Doubts");

            migrationBuilder.DropColumn(
                name: "AnswerProvider",
                table: "Doubts");

            migrationBuilder.DropColumn(
                name: "Evidence",
                table: "Doubts");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Doubts");

            migrationBuilder.AlterColumn<int>(
                name: "AnchorStartIndex",
                table: "Doubts",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AnchorExactQuote",
                table: "Doubts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AnchorEndIndex",
                table: "Doubts",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AnchorBlockId",
                table: "Doubts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
