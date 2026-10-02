using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// Ngôn ngữ câu hỏi AI Coach: profile (bước CV) và job sinh đề (Hangfire đọc sau request).
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20261003003000_AddCoachOutputLanguage")]
public partial class AddCoachOutputLanguage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tbl_candidate_profiles
                ADD COLUMN IF NOT EXISTS "CoachOutputLanguage" character varying(20) NULL;
            ALTER TABLE tbl_candidate_personal_set_jobs
                ADD COLUMN IF NOT EXISTS "OutputLanguage" character varying(20) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tbl_candidate_personal_set_jobs
                DROP COLUMN IF EXISTS "OutputLanguage";
            ALTER TABLE tbl_candidate_profiles
                DROP COLUMN IF EXISTS "CoachOutputLanguage";
            """);
    }
}
