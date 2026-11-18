using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteLab.Migrations
{
    /// <inheritdoc />
    public partial class AddResearchSourceVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "ResearchSources",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);

            // ADR-045: as fontes já gravadas eram todas links da busca do provedor → conferidas.
            migrationBuilder.AddColumn<bool>(
                name: "Verified",
                table: "ResearchSources",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Origens declaradas (sem link) não existem no esquema antigo.
            migrationBuilder.Sql("DELETE FROM \"ResearchSources\" WHERE \"Url\" IS NULL;");

            migrationBuilder.DropColumn(
                name: "Verified",
                table: "ResearchSources");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "ResearchSources",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048,
                oldNullable: true);
        }
    }
}
