namespace DomainLayer.Constants;

/// <summary>SCRUM — loại / trạng thái assessment Coach competency.</summary>
public static class CandidateAssessmentKind
{
    public const string Diagnostic = "Diagnostic";
    public const string Reassessment = "Reassessment";
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
