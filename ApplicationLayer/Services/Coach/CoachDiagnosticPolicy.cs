using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-506: đọc + clamp số liệu đề chẩn đoán / sàng lọc từ policy singleton.
/// Cột mới = 0 (payload cũ / DB chưa migrate) → rơi về mặc định hành vi hiện tại.
/// </summary>
public static class CoachDiagnosticPolicy
{
    public const int DefaultQuestionsPerSkill = 3;
    public const int DefaultMinSkills = 3;
    public const int DefaultMaxSkills = 5;
    public const int DefaultMaxAdaptiveSkills = 8;
    public const int DefaultScreeningPerSkill = 1;
    public const int DefaultScreeningMaxSkills = 12;
    public const int DefaultReassessmentPerSkill = 3;

    public static int QuestionsPerSkill(CompetencyScoringPolicy? p)
        => Clamp(p?.DiagnosticQuestionsPerSkill ?? 0, 2, 6, DefaultQuestionsPerSkill);

    public static int MinSkills(CompetencyScoringPolicy? p)
        => Clamp(p?.DiagnosticMinSkills ?? 0, 1, 12, DefaultMinSkills);

    public static int MaxSkills(CompetencyScoringPolicy? p)
    {
        var min = MinSkills(p);
        var max = Clamp(p?.DiagnosticMaxSkills ?? 0, 1, 12, DefaultMaxSkills);
        return Math.Max(min, max);
    }

    public static int MaxAdaptiveSkills(CompetencyScoringPolicy? p)
        => Clamp(p?.DiagnosticMaxAdaptiveSkills ?? 0, 1, 20, DefaultMaxAdaptiveSkills);

    public static int MinTotalQuestions(CompetencyScoringPolicy? p)
        => Clamp(p?.DiagnosticMinTotalQuestions ?? 0, 0, 60, 0);

    public static bool ScreeningEnabled(CompetencyScoringPolicy? p)
        => p is null || p.ScreeningEnabled;

    public static int ScreeningQuestionsPerSkill(CompetencyScoringPolicy? p)
        => Clamp(p?.ScreeningQuestionsPerSkill ?? 0, 1, 3, DefaultScreeningPerSkill);

    public static int ScreeningMaxSkills(CompetencyScoringPolicy? p)
        => Clamp(p?.ScreeningMaxSkills ?? 0, 1, 20, DefaultScreeningMaxSkills);

    /// <summary>SCRUM-508: bài Đánh giá lại 1 skill — tối thiểu 2 câu evidence, trần 10.</summary>
    public static int ReassessmentQuestionsPerSkill(CompetencyScoringPolicy? p)
        => Clamp(p?.ReassessmentQuestionsPerSkill ?? 0, 2, 10, DefaultReassessmentPerSkill);

    /// <summary>
    /// Diagnostic cần ≥2 câu/skill để có evidence. Nếu admin bật min tổng câu
    /// thì tăng per-skill (trần 6) thay vì kéo thêm skill phụ.
    /// </summary>
    public static int ResolveDiagnosticQuestionsPerSkill(CompetencyScoringPolicy? p, int skillCount)
    {
        var perSkill = QuestionsPerSkill(p);
        var minTotal = MinTotalQuestions(p);
        if (minTotal <= 0 || skillCount <= 0) return perSkill;
        var total = skillCount * perSkill;
        if (total >= minTotal) return perSkill;
        var raised = (int)Math.Ceiling(minTotal / (double)skillCount);
        return Math.Clamp(raised, 2, 6);
    }

    private static int Clamp(int value, int min, int max, int fallback)
        => value <= 0 ? fallback : Math.Clamp(value, min, max);
}
