using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <summary>
    /// Aligns EF model (V1 entities Ignored). Tables already dropped by DropV1QuestionGenerationTables —
    /// Up is idempotent.
    /// </summary>
    public partial class IgnoreV1EntitiesSync : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE IF EXISTS question_sets
                  DROP CONSTRAINT IF EXISTS "FK_question_sets_question_generation_jobs_SourceJobId";
                DROP TABLE IF EXISTS question_ai_chat_messages CASCADE;
                DROP TABLE IF EXISTS generated_questions CASCADE;
                DROP TABLE IF EXISTS question_generation_plans CASCADE;
                DROP TABLE IF EXISTS question_generation_jobs CASCADE;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore from backup_db full dump if needed.
        }
    }
}
