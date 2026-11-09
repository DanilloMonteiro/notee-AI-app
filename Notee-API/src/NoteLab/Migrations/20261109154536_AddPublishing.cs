using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Publications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ShareToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CurrentRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Publications_Notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicProfiles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Handle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_PublicProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicationRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteVersion = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicationRevisions_Publications_PublicationId",
                        column: x => x.PublicationId,
                        principalTable: "Publications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicationRevisions_PublicationId_PublishedAt",
                table: "PublicationRevisions",
                columns: new[] { "PublicationId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Publications_NoteId",
                table: "Publications",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_OwnerId_Slug",
                table: "Publications",
                columns: new[] { "OwnerId", "Slug" },
                unique: true,
                filter: "\"Status\" = 'Published'");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_ShareToken",
                table: "Publications",
                column: "ShareToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicProfiles_Handle",
                table: "PublicProfiles",
                column: "Handle",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicationRevisions");

            migrationBuilder.DropTable(
                name: "PublicProfiles");

            migrationBuilder.DropTable(
                name: "Publications");
        }
    }
}
