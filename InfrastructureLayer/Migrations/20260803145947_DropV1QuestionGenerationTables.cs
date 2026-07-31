using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class DropV1QuestionGenerationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_studio_question_generation_runs_MirroredJobId",
                table: "studio_question_generation_runs");

            migrationBuilder.DropColumn(
                name: "MirroredJobId",
                table: "studio_question_generation_runs");

            // Break FK question_sets → jobs before drop (was Restrict / later SetNull)
            migrationBuilder.Sql(
                """
                ALTER TABLE IF EXISTS question_sets
                  DROP CONSTRAINT IF EXISTS "FK_question_sets_question_generation_jobs_SourceJobId";
                """);

            // Drop order: children first
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS question_ai_chat_messages CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS generated_questions CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS question_generation_plans CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS question_generation_jobs CASCADE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không recreate full V1 schema trong Down — restore từ backup_db full dump.
            migrationBuilder.AddColumn<Guid>(
                name: "MirroredJobId",
                table: "studio_question_generation_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_studio_question_generation_runs_MirroredJobId",
                table: "studio_question_generation_runs",
                column: "MirroredJobId");
        }
    }
}
