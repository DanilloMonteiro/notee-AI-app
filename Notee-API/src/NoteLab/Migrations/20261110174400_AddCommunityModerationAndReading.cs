using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityModerationAndReading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PublicationComments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "PublicationComments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsModerator",
                table: "CommunityMembers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                table: "CommunityMembers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PublicationViews",
                columns: table => new
                {
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationViews", x => new { x.PublicationId, x.UserId, x.Day });
                    table.ForeignKey(
                        name: "FK_PublicationViews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PublicationViews_Publications_PublicationId",
                        column: x => x.PublicationId,
                        principalTable: "Publications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicationComments_ParentId",
                table: "PublicationComments",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationViews_UserId",
                table: "PublicationViews",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PublicationComments_PublicationComments_ParentId",
                table: "PublicationComments",
                column: "ParentId",
                principalTable: "PublicationComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PublicationComments_PublicationComments_ParentId",
                table: "PublicationComments");

            migrationBuilder.DropTable(
                name: "PublicationViews");

            migrationBuilder.DropIndex(
                name: "IX_PublicationComments_ParentId",
                table: "PublicationComments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PublicationComments");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "PublicationComments");

            migrationBuilder.DropColumn(
                name: "IsModerator",
                table: "CommunityMembers");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                table: "CommunityMembers");
        }
    }
}
