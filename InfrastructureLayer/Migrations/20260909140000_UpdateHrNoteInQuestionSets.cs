using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// Data-only: xóa marker STUDIO_SAVE / STUDIO_MIRROR khỏi HrNote (đã lộ ra Marketplace description).
/// Không đổi schema — SourceProjectId vẫn là liên kết Studio.
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260909140000_UpdateHrNoteInQuestionSets")]
public partial class UpdateHrNoteInQuestionSets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "tbl_question_sets"
            SET "HrNote" = NULL
            WHERE "HrNote" ILIKE 'STUDIO_SAVE%'
               OR "HrNote" ILIKE 'STUDIO_MIRROR%';
            """);
    }

    /// <summary>Không khôi phục được mô tả cũ đã ghi đè bằng marker.</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
