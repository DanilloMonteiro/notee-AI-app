using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Boards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NoteReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Author = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    Publisher = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    PublicationYear = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteReferences_Notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardEdges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardEdges", x => x.Id);
                    table.CheckConstraint("CK_BoardEdges_NoSelfEdge", "\"FromNoteId\" <> \"ToNoteId\"");
                    table.ForeignKey(
                        name: "FK_BoardEdges_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardEdges_Notes_FromNoteId",
                        column: x => x.FromNoteId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardEdges_Notes_ToNoteId",
                        column: x => x.ToNoteId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardPlacements",
                columns: table => new
                {
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    X = table.Column<double>(type: "double precision", nullable: false),
                    Y = table.Column<double>(type: "double precision", nullable: false),
                    PositionVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardPlacements", x => new { x.BoardId, x.NoteId });
                    table.CheckConstraint("CK_BoardPlacements_Coordinates", "\"X\" BETWEEN -1000 AND 7000 AND \"Y\" BETWEEN -1000 AND 7000");
                    table.ForeignKey(
                        name: "FK_BoardPlacements_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardPlacements_Notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardEdges_BoardId_FromNoteId",
                table: "BoardEdges",
                columns: new[] { "BoardId", "FromNoteId" });

            migrationBuilder.CreateIndex(
                name: "IX_BoardEdges_BoardId_ToNoteId",
                table: "BoardEdges",
                columns: new[] { "BoardId", "ToNoteId" });

            migrationBuilder.CreateIndex(
                name: "IX_BoardEdges_FromNoteId",
                table: "BoardEdges",
                column: "FromNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardEdges_ToNoteId",
                table: "BoardEdges",
                column: "ToNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardPlacements_NoteId",
                table: "BoardPlacements",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_OwnerId_UpdatedAt",
                table: "Boards",
                columns: new[] { "OwnerId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteReferences_NoteId",
                table: "NoteReferences",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteReferences_OwnerId_NoteId",
                table: "NoteReferences",
                columns: new[] { "OwnerId", "NoteId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardEdges");

            migrationBuilder.DropTable(
                name: "BoardPlacements");

            migrationBuilder.DropTable(
                name: "NoteReferences");

            migrationBuilder.DropTable(
                name: "Boards");
        }
    }
}
