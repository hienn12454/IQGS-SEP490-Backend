using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddAiInsightToPracticeSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiInsightEn",
                table: "practice_sessions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiInsightVi",
                table: "practice_sessions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkillsToImproveJson",
                table: "practice_sessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiInsightEn",
                table: "practice_sessions");

            migrationBuilder.DropColumn(
                name: "AiInsightVi",
                table: "practice_sessions");

            migrationBuilder.DropColumn(
                name: "SkillsToImproveJson",
                table: "practice_sessions");
        }
    }
}
