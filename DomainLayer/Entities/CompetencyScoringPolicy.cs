namespace DomainLayer.Entities;

/// <summary>Singleton config công thức chấm Coach — không hardcode rải source.</summary>
public class CompetencyScoringPolicy : BaseEntity
{
    public static readonly Guid SingletonId = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    public double CorrectnessWeight { get; set; } = 0.5;
    public double RelevanceWeight { get; set; } = 0.3;
    public double ClarityWeight { get; set; } = 0.2;

    public double EasyDifficultyWeight { get; set; } = 1.0;
    public double MediumDifficultyWeight { get; set; } = 1.5;
    public double HardDifficultyWeight { get; set; } = 2.0;

    public double DevelopingMaxExclusive { get; set; } = 50;
    public double NearTargetMaxExclusive { get; set; } = 70;
    public double ReadyMaxExclusive { get; set; } = 85;
    // Strong: >= ReadyMaxExclusive

    /// <summary>% core skills đạt target để Junior Ready.</summary>
    public double JuniorReadyCoreSkillRatio { get; set; } = 0.7;
    public double OverallReadyThreshold { get; set; } = 70;

    /// <summary>
    /// SCRUM-457: JSON {"Fresher":60,"Junior":70,...}. Null/thiếu key → fallback OverallReadyThreshold.
    /// Adaptive TargetScore đi qua CompetencyTargetScorePolicy.Resolve — không đổi scoring engine.
    /// </summary>
    public string? TargetScoreByLevelJson { get; set; }

    // ── SCRUM-488: Admin cấu hình drill AI Coach (singleton cùng scoring policy) ──

    /// <summary>Qua topic khi DrillScore &gt; giá trị này (exclusive min).</summary>
    public double DrillPassScoreExclusiveMin { get; set; } = 70;

    public int DrillQuestionCountWeak { get; set; } = 20;
    public int DrillQuestionCountMid { get; set; } = 15;
    public int DrillQuestionCountStrong { get; set; } = 10;

    /// <summary>current &lt; target × ratio → band yếu.</summary>
    public double DrillWeakBandRatio { get; set; } = 0.6;

    public bool DrillRemixEnabled { get; set; } = true;
    public double DrillRemixRatio { get; set; } = 0.35;

    /// <summary>Câu có điểm AI &lt; ngưỡng này vào pool remix.</summary>
    public double DrillWeakAnswerScoreMaxExclusive { get; set; } = 50;

    // ── SCRUM-506: Admin cấu hình độ rộng đề chẩn đoán + bài sàng lọc ──

    /// <summary>Số câu mỗi core skill ở bài chẩn đoán (mặc định 3).</summary>
    public int DiagnosticQuestionsPerSkill { get; set; } = 3;
    public int DiagnosticMinSkills { get; set; } = 3;
    public int DiagnosticMaxSkills { get; set; } = 5;
    public int DiagnosticMaxAdaptiveSkills { get; set; } = 8;
    /// <summary>0 = tắt. Nếu tổng câu &lt; ngưỡng thì tăng câu/skill (trần 6).</summary>
    public int DiagnosticMinTotalQuestions { get; set; } = 0;

    public bool ScreeningEnabled { get; set; } = true;
    public int ScreeningQuestionsPerSkill { get; set; } = 1;
    public int ScreeningMaxSkills { get; set; } = 12;
}
