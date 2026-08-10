namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Domain policy quy đổi hoạt động luyện tập → XP. Toàn bộ số liệu đọc từ GamificationOptions —
/// không magic number rải rác trong service/controller. Pure — không I/O.
/// </summary>
public interface IXpRewardPolicy
{
    /// <summary>XP cố định khi 1 câu hỏi được hoàn thành (có kết quả AI chấm Succeeded).</summary>
    int QuestionCompletedXp { get; }

    /// <summary>XP cố định khi hoàn thành 1 phiên luyện tập (question set).</summary>
    int QuestionSetCompletedXp { get; }

    /// <summary>XP cố định cho improvement bonus.</summary>
    int ImprovementBonusXp { get; }

    /// <summary>Điểm thưởng theo tier: &lt;60→0, 60-74→2, 75-89→4, &gt;=90→6 (mặc định — xem GamificationOptions).</summary>
    /// <exception cref="ArgumentOutOfRangeException">score ngoài [0, 100].</exception>
    int GetScoreBonus(double score);

    /// <summary>True nếu currentScore cải thiện so với previousScore đủ ngưỡng (mặc định &gt;= 10 điểm %).</summary>
    bool IsImprovementEligible(double previousScore, double currentScore);

    /// <summary>True nếu đúng mốc streak được cấu hình thưởng (3/7/14/30 mặc định), kèm số XP tương ứng.</summary>
    bool TryGetStreakMilestoneXp(int streakDays, out int xp);
}
