using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// SCRUM-508: số câu bài Đánh giá lại độc lập diagnostic trên tbl_competency_scoring_policies.
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20261001173500_AddReassessmentQuestionsPerSkill")]
public partial class AddReassessmentQuestionsPerSkill : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "ReassessmentQuestionsPerSkill" integer NOT NULL DEFAULT 3;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tbl_competency_scoring_policies
                DROP COLUMN IF EXISTS "ReassessmentQuestionsPerSkill";
            """);
    }
}
