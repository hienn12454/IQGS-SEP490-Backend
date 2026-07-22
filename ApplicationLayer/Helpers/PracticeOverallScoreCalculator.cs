namespace ApplicationLayer.Helpers;

/// <summary>
/// Công thức overallScore phiên luyện tập (SCRUM-304):
/// tổng Score Succeeded / tổng số câu trong bộ — câu chưa làm hoặc Failed = 0.
/// </summary>
public static class PracticeOverallScoreCalculator
{
    /// <summary>
    /// Tính overall. <paramref name="succeededScores"/> chỉ gồm điểm các câu evaluate Succeeded.
    /// Trả null nếu <paramref name="totalQuestionCount"/> ≤ 0.
    /// </summary>
    public static double? Compute(IEnumerable<double> succeededScores, int totalQuestionCount)
    {
        if (totalQuestionCount <= 0)
            return null;

        var sum = succeededScores.Sum();
        // Làm tròn 2 chữ số thập phân (vd 3.17) — trực quan hơn số nguyên khi overall là 1 phân số nhỏ trên tổng số câu.
        return Math.Round(sum / totalQuestionCount, 2);
    }
}
