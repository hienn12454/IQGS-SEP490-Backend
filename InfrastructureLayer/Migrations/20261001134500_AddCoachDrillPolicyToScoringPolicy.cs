using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// SCRUM-488: thêm cột drill policy vào tbl_competency_scoring_policies
/// (Admin /admin/settings → AI Coach lưu số câu / remix / pass).
/// Idempotent: DB có thể đã có cột từ lần apply tay / deploy trước — dùng IF NOT EXISTS.
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20261001134500_AddCoachDrillPolicyToScoringPolicy")]
public partial class AddCoachDrillPolicyToScoringPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DrillPassScoreExclusiveMin" double precision NOT NULL DEFAULT 70.0;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DrillQuestionCountWeak" integer NOT NULL DEFAULT 20;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DrillQuestionCountMid" integer NOT NULL DEFAULT 15;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DrillQuestionCountStrong" integer NOT NULL DEFAULT 10;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DrillWeakBandRatio" double precision NOT NULL DEFAULT 0.6;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DrillRemixEnabled" boolean NOT NULL DEFAULT true;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DrillRemixRatio" double precision NOT NULL DEFAULT 0.35;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DrillWeakAnswerScoreMaxExclusive" double precision NOT NULL DEFAULT 50.0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tbl_competency_scoring_policies
                DROP COLUMN IF EXISTS "DrillPassScoreExclusiveMin",
                DROP COLUMN IF EXISTS "DrillQuestionCountWeak",
                DROP COLUMN IF EXISTS "DrillQuestionCountMid",
                DROP COLUMN IF EXISTS "DrillQuestionCountStrong",
                DROP COLUMN IF EXISTS "DrillWeakBandRatio",
                DROP COLUMN IF EXISTS "DrillRemixEnabled",
                DROP COLUMN IF EXISTS "DrillRemixRatio",
                DROP COLUMN IF EXISTS "DrillWeakAnswerScoreMaxExclusive";
            """);
    }
}
