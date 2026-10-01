namespace DomainLayer.Constants;

using DomainLayer.Entities;

/// <summary>SCRUM — loại / trạng thái assessment Coach competency.</summary>
public static class CandidateAssessmentKind
{
    public const string Diagnostic = "Diagnostic";
    public const string Reassessment = "Reassessment";
    /// <summary>SCRUM-506: bài sàng lọc ngắn cho skill CV chưa đo — không merge vào profile/level.</summary>
    public const string Screening = "Screening";
}

public static class CandidateAssessmentStatus
{
    public const string PendingGeneration = "PendingGeneration";
    public const string ReadyToPractice = "ReadyToPractice";
    public const string InProgress = "InProgress";
    public const string Scored = "Scored";
    public const string Failed = "Failed";
    /// <summary>Bị thay thế khi Candidate chạy diagnostic mới / huỷ job.</summary>
    public const string Abandoned = "Abandoned";
    /// <summary>SCRUM-459: assessment đã Scored bị thay bởi vòng Coach mới (giữ lịch sử).</summary>
    public const string Superseded = "Superseded";
}

public static class CompetencyReadinessStatus
{
    public const string Developing = "Developing";
    public const string NearTarget = "NearTarget";
    public const string Ready = "Ready";
    public const string Strong = "Strong";
}

public static class CandidateRoadmapItemStatus
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string ReadyForReassessment = "ReadyForReassessment";
}

/// <summary>
/// SCRUM-487/488: drill topic chỉ Completed khi OverallScore &gt; ngưỡng (không phải ≥).
/// Ngưỡng lấy từ CompetencyScoringPolicy.DrillPassScoreExclusiveMin (Admin cấu hình).
/// </summary>
public static class CoachDrillPassPolicy
{
    public const double DefaultPassScoreExclusiveMin = 70;

    public static bool IsPassing(double? overallScore, double exclusiveMin = DefaultPassScoreExclusiveMin)
        => overallScore is double s && s > exclusiveMin;

    /// <summary>SCRUM-488: số câu drill theo band competency hiện tại.</summary>
    public static int ResolveQuestionCount(
        CompetencyScoringPolicy policy, double? currentScore, double targetScore)
    {
        var band = currentScore ?? 0;
        var target = targetScore > 0 ? targetScore : 70;
        var weakRatio = policy.DrillWeakBandRatio is > 0 and <= 1
            ? policy.DrillWeakBandRatio
            : 0.6;
        int count;
        if (band < target * weakRatio)
            count = policy.DrillQuestionCountWeak;
        else if (band < target)
            count = policy.DrillQuestionCountMid;
        else
            count = policy.DrillQuestionCountStrong;

        if (count < 5) count = 5;
        if (count > 40) count = 40;
        return count;
    }
}

public static class CandidateRoadmapStatus
{
    public const string Suggested = "Suggested";
    public const string Active = "Active";
    public const string Completed = "Completed";
}

/// <summary>SCRUM-455: gap = skill chưa đạt target; advanced = đã đạt nhưng vẫn luyện tiếp.</summary>
public static class CandidateRoadmapKind
{
    public const string Gap = "gap";
    public const string Advanced = "advanced";
}

public static class QuestionDifficultyLevel
{
    public const string Easy = "easy";
    public const string Medium = "medium";
    public const string Hard = "hard";
}

public static class CoachSeniorityLevel
{
    public const string Fresher = "Fresher";
    public const string Junior = "Junior";
    public const string Middle = "Middle";
    public const string Senior = "Senior";

    public static readonly string[] All = [Fresher, Junior, Middle, Senior];

    public static bool IsValid(string? level)
        => All.Any(l => string.Equals(l, (level ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>SCRUM-453 — nguồn gốc số liệu framework (phân biệt seed MVP vs curated data).</summary>
public static class CompetencyFrameworkProvenance
{
    /// <summary>Seed MVP ban đầu, số liệu chưa qua thẩm định của nhóm.</summary>
    public const string Provisional = "Provisional";
    /// <summary>Import từ bộ curated data (có sourceRef/sourceVersion).</summary>
    public const string Curated = "Curated";
}

public static class CompetencyFrameworkStatus
{
    public const string Active = "Active";
    public const string Draft = "Draft";
}

public static class CompetencyRoleAliasMatchKind
{
    public const string Exact = "exact";
    public const string Contains = "contains";
}

/// <summary>SCRUM-457: FRAMEWORK | ADAPTIVE | UNSUPPORTED — không hardcode family list.</summary>
public static class CompetencyResolutionMode
{
    public const string Framework = "FRAMEWORK";
    public const string Adaptive = "ADAPTIVE";
    public const string Unsupported = "UNSUPPORTED";
}

public static class CompetencySourceMode
{
    public const string Framework = "framework";
    public const string Rag = "rag";
    public const string RagDynamic = "rag_dynamic";
}

public static class CompetencyCategory
{
    public const string Fundamental = "FUNDAMENTAL";
    public const string RoleCore = "ROLE_CORE";
    public const string Advanced = "ADVANCED";
}

public static class CompetencyTargetReadinessStatus
{
    public const string Ready = "READY";
    public const string NotReady = "NOT_READY";
}

public static class CompetencyBlueprintSchema
{
    public const int CurrentVersion = 1;
}
