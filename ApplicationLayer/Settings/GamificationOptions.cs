namespace ApplicationLayer.Settings;

/// <summary>
/// Cấu hình tập trung mọi con số nghiệp vụ của hệ Gamification — KHÔNG được hard-code rải rác
/// trong service/controller. Đọc từ appsettings section "Gamification"; mọi property có default
/// khớp đúng business rules ban đầu, dùng được ngay cả khi thiếu section trong config.
/// </summary>
public class GamificationOptions
{
    public const string SectionName = "Gamification";

    // ── XP cơ bản ────────────────────────────────────────────────────
    public int QuestionCompletedXp { get; set; } = 10;
    public int QuestionSetCompletedXp { get; set; } = 20;

    // ── Improvement bonus ───────────────────────────────────────────
    public int ImprovementBonusXp { get; set; } = 5;

    /// <summary>Số điểm % tối thiểu phải cải thiện so với lần trước để nhận Improvement bonus.</summary>
    public double ImprovementBonusMinDeltaPoints { get; set; } = 10;

    // ── Score bonus (điểm AI chấm 0-100) ────────────────────────────
    public List<ScoreBonusTier> ScoreBonusTiers { get; set; } =
    [
        new ScoreBonusTier { MinScoreInclusive = 90, Xp = 6 },
        new ScoreBonusTier { MinScoreInclusive = 75, Xp = 4 },
        new ScoreBonusTier { MinScoreInclusive = 60, Xp = 2 },
        new ScoreBonusTier { MinScoreInclusive = 0, Xp = 0 }
    ];

    // ── Streak milestones: số ngày streak → Xp thưởng ───────────────
    public Dictionary<int, int> StreakMilestoneXp { get; set; } = new()
    {
        [3] = 20,
        [7] = 50,
        [14] = 100,
        [30] = 200
    };

    // ── Level formula: RequiredXp(level) = LevelBaseXp + (level - 1) * LevelXpIncrement ──
    public long LevelBaseXp { get; set; } = 100;
    public long LevelXpIncrement { get; set; } = 50;

    // ── Daily goal ───────────────────────────────────────────────────
    public int DefaultDailyGoalXp { get; set; } = 50;
    public List<int> AllowedDailyGoalXpValues { get; set; } = [20, 50, 80, 120];

    // ── Activity API — giới hạn range để tránh abusive request ──────
    public int MaxActivityRangeDays { get; set; } = 366;
}

public class ScoreBonusTier
{
    /// <summary>Điểm tối thiểu (inclusive) để nhận mức thưởng này.</summary>
    public double MinScoreInclusive { get; set; }
    public int Xp { get; set; }
}
