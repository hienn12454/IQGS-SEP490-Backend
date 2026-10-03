using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// Lưu điểm từng tiêu chí rubric HR cho mỗi câu trả lời (practice + hiring).
/// Cột nullable: câu cũ / Coach / rubric không hợp lệ vẫn để NULL = chấm tổng thể, không cần backfill.
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20261003120000_AddCriteriaScoresJsonToAiFeedback")]
public partial class AddCriteriaScoresJsonToAiFeedback : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tbl_ai_feedbacks
                ADD COLUMN IF NOT EXISTS "CriteriaScoresJson" jsonb NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tbl_ai_feedbacks
                DROP COLUMN IF EXISTS "CriteriaScoresJson";
            """);
    }
}
