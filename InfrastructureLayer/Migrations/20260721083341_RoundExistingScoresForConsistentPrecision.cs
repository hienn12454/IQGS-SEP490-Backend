using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <summary>
    /// Data-only migration (không đổi schema): làm tròn về đúng độ chính xác hiển thị hiện tại các điểm số đã lưu
    /// TRƯỚC khi có fix làm tròn ở tầng ứng dụng — những dòng cũ này vẫn còn nguyên số thập phân dài
    /// (vd 51.0739292829) do được ghi trước khi có Math.Round.
    /// - practice_sessions.OverallScore / candidate_recommendations.OverallScore: 2 chữ số thập phân
    ///   (khớp PracticeOverallScoreCalculator.Compute, vd 3.17).
    /// - ai_feedbacks.Score (điểm AI chấm từng câu): hàng đơn vị (khớp CandidatePracticeSessionService.RoundScore).
    /// Idempotent — chạy lại nhiều lần không đổi kết quả.
    /// </summary>
    public partial class RoundExistingScoresForConsistentPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE practice_sessions
                SET ""OverallScore"" = ROUND(""OverallScore""::numeric, 2)
                WHERE ""OverallScore"" IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE ai_feedbacks
                SET ""Score"" = ROUND(""Score""::numeric, 0)
                WHERE ""Score"" IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE candidate_recommendations
                SET ""OverallScore"" = ROUND(""OverallScore""::numeric, 2);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không thể khôi phục độ chính xác thập phân gốc đã làm tròn — no-op có chủ đích.
        }
    }
}
