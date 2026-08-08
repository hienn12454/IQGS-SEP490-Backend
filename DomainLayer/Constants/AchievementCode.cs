namespace DomainLayer.Constants;

/// <summary>Mã achievement cố định — code-defined (không dùng bảng AchievementDefinition), khớp IAchievementRule.Code.</summary>
public static class AchievementCode
{
    /// <summary>Hoàn thành phiên luyện tập đầu tiên.</summary>
    public const string FirstStep = "FIRST_STEP";

    /// <summary>Đạt streak 7 ngày.</summary>
    public const string OnFire = "ON_FIRE";

    /// <summary>Đạt điểm &gt;= 90 ở 1 câu đã hoàn thành.</summary>
    public const string ExcellentAnswer = "EXCELLENT_ANSWER";

    /// <summary>Hoàn thành 100 câu hỏi.</summary>
    public const string Dedicated = "DEDICATED";

    /// <summary>Hoàn thành 50 câu hỏi Technical.</summary>
    public const string TechnicalMind = "TECHNICAL_MIND";

    /// <summary>Hoàn thành 30 câu hỏi System Design.</summary>
    public const string SystemThinker = "SYSTEM_THINKER";

    /// <summary>Đạt daily goal ở 10 ngày (local date) khác nhau.</summary>
    public const string Consistency = "CONSISTENCY";

    /// <summary>Hoàn thành 100 phiên luyện tập.</summary>
    public const string InterviewVeteran = "INTERVIEW_VETERAN";
}
