using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddDoubtAnswer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Answer",
                table: "Doubts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Answer",
                table: "Doubts");
        }
    }
}
