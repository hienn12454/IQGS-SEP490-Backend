using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// SCRUM-506: cột cấu hình đề chẩn đoán + bài sàng lọc trên tbl_competency_scoring_policies.
/// Idempotent: IF NOT EXISTS — an toàn khi DB đã apply tay.
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20261001170000_AddDiagnosticScreeningPolicy")]
public partial class AddDiagnosticScreeningPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DiagnosticQuestionsPerSkill" integer NOT NULL DEFAULT 3;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DiagnosticMinSkills" integer NOT NULL DEFAULT 3;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DiagnosticMaxSkills" integer NOT NULL DEFAULT 5;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DiagnosticMaxAdaptiveSkills" integer NOT NULL DEFAULT 8;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "DiagnosticMinTotalQuestions" integer NOT NULL DEFAULT 0;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "ScreeningEnabled" boolean NOT NULL DEFAULT true;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "ScreeningQuestionsPerSkill" integer NOT NULL DEFAULT 1;
            ALTER TABLE tbl_competency_scoring_policies
                ADD COLUMN IF NOT EXISTS "ScreeningMaxSkills" integer NOT NULL DEFAULT 12;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tbl_competency_scoring_policies
                DROP COLUMN IF EXISTS "DiagnosticQuestionsPerSkill",
                DROP COLUMN IF EXISTS "DiagnosticMinSkills",
                DROP COLUMN IF EXISTS "DiagnosticMaxSkills",
                DROP COLUMN IF EXISTS "DiagnosticMaxAdaptiveSkills",
                DROP COLUMN IF EXISTS "DiagnosticMinTotalQuestions",
                DROP COLUMN IF EXISTS "ScreeningEnabled",
                DROP COLUMN IF EXISTS "ScreeningQuestionsPerSkill",
                DROP COLUMN IF EXISTS "ScreeningMaxSkills";
            """);
    }
}
