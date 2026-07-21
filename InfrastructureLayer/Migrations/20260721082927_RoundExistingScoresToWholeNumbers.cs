using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <summary>
    /// Data-only migration (không đổi schema): làm tròn về hàng đơn vị các điểm số đã lưu TRƯỚC khi áp dụng
    /// fix làm tròn ở tầng ứng dụng (PracticeSession.OverallScore, AiFeedback.Score, CandidateRecommendation.OverallScore) —
    /// những dòng cũ này vẫn còn nguyên số thập phân dài (vd 51.0739292829) do được ghi trước khi có Math.Round.
    /// Idempotent — chạy lại nhiều lần không đổi kết quả vì ROUND(x) của 1 số nguyên vẫn là chính nó.
    /// </summary>
    public partial class RoundExistingScoresToWholeNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE practice_sessions
                SET ""OverallScore"" = ROUND(""OverallScore""::numeric, 0)
                WHERE ""OverallScore"" IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE ai_feedbacks
                SET ""Score"" = ROUND(""Score""::numeric, 0)
                WHERE ""Score"" IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE candidate_recommendations
                SET ""OverallScore"" = ROUND(""OverallScore""::numeric, 0);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không thể khôi phục độ chính xác thập phân gốc đã làm tròn — no-op có chủ đích.
        }
    }
}
