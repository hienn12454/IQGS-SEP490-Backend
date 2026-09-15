using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Candidate;

public class CreateCvDrillDto
{
    [Required]
    [MaxLength(200)]
    public string Skill { get; set; } = string.Empty;
}

public class CandidateSkillPlanDto
{
    public Guid Id { get; set; }
    public Guid? SourceDiagnosticSetId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<CandidateSkillPlanItemDto> Items { get; set; } = new();
}

public class CandidateSkillPlanItemDto
{
    public Guid Id { get; set; }
    public string Skill { get; set; } = string.Empty;
    public double? BaselineScore { get; set; }
    public double? CurrentScore { get; set; }
    public double TargetScore { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>SCRUM-447: context Coach trước diagnostic.</summary>
public class CoachContextDto
{
    public string? SuggestedRole { get; set; }
    public string? TargetRole { get; set; }
    public string? SelfAssessedLevel { get; set; }
    public string? TargetLevel { get; set; }
    public double? YearsOfExperience { get; set; }
    public string? InterviewGoal { get; set; }
    public string? Summary { get; set; }
    public List<string> Skills { get; set; } = new();
    public bool ContextConfirmed { get; set; }
    public DateTime? ContextConfirmedAt { get; set; }
    public bool HasCv { get; set; }
    public string? MatchedFrameworkRole { get; set; }
    public string? MatchedFrameworkLevel { get; set; }
    public Guid? MatchedFrameworkId { get; set; }
    /// <summary>SCRUM-453: công nghệ chính của framework đã khớp (vd "ASP.NET Core").</summary>
    public string? MatchedFrameworkTechnology { get; set; }
    /// <summary>false = target role chưa có framework -> FE cảnh báo ngay ở bước Confirm Goal.</summary>
    public bool FrameworkResolved { get; set; }
    /// <summary>true = có framework cho role nhưng không đúng level yêu cầu (đang dùng level gần nhất).</summary>
    public bool FrameworkLevelFallback { get; set; }
    /// <summary>Catalog role/level đang được hỗ trợ để FE gợi ý thay vì để backend throw lúc Start Diagnostic.</summary>
    public List<CoachFrameworkOptionDto> AvailableFrameworks { get; set; } = new();
    /// <summary>SCRUM-457: FRAMEWORK | ADAPTIVE | UNSUPPORTED.</summary>
    public string ResolutionMode { get; set; } = "UNSUPPORTED";
    public string? RoleFamilyKey { get; set; }
    public string? RoleFamilyDisplay { get; set; }
    public double ResolutionConfidence { get; set; }
    public string? ResolutionReason { get; set; }
    public List<string> SupportedRoles { get; set; } = new();
    public List<string> DetectedSkills { get; set; } = new();
}

/// <summary>SCRUM-453: một vai trò trong catalog framework (data-driven, không hardcode stack).</summary>
public class CoachFrameworkOptionDto
{
    public string RoleKey { get; set; } = string.Empty;
    public string DisplayRole { get; set; } = string.Empty;
    public string? Technology { get; set; }
    public List<string> Levels { get; set; } = new();
    /// <summary>Provisional | Curated — FE có thể hiển thị nhãn "dữ liệu tạm".</summary>
    public string Provenance { get; set; } = string.Empty;
}

public class UpdateCoachContextDto
{
    [MaxLength(200)]
    public string? TargetRole { get; set; }

    [MaxLength(30)]
    public string? SelfAssessedLevel { get; set; }

    [MaxLength(30)]
    public string? TargetLevel { get; set; }

    public double? YearsOfExperience { get; set; }

    // SCRUM-458: InterviewGoal / Skills không còn nhận từ Confirm Goal (giữ cột DB cũ).
}

public class CoachAssessmentDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? JobId { get; set; }
    public Guid? QuestionSetId { get; set; }
    public Guid? PracticeSessionId { get; set; }
    public double? OverallReadiness { get; set; }
    public string? ReadinessStatus { get; set; }
    public bool MeetsJuniorReadyRule { get; set; }
    public string? FrameworkDisplayRole { get; set; }
    public string? FrameworkTargetLevel { get; set; }
    public List<CoachSkillResultDto> Skills { get; set; } = new();
    public string? Explanation { get; set; }
    public double? PreviousOverallReadiness { get; set; }
    public double? OverallDelta { get; set; }
    /// <summary>Level cao nhất thoả rule global trên competency profile (không phải LLM).</summary>
    public string? AchievedLevel { get; set; }
    public string? LevelExplanation { get; set; }
    public double? CoverageRatio { get; set; }
    /// <summary>SCRUM-457: Target Readiness — không dùng AchievedLevel làm headline seniority.</summary>
    public string? ResolutionMode { get; set; }
    public string? TargetLevel { get; set; }
    public double? TargetThreshold { get; set; }
    public double? ReadinessPercent { get; set; }
    public string? EstimatedBand { get; set; }
    public string? TargetReadinessStatus { get; set; }
    public List<CoachSkillGapDto> SkillGaps { get; set; } = new();
}

public class CoachSkillGapDto
{
    public string Skill { get; set; } = string.Empty;
    public double CurrentScore { get; set; }
    public double TargetScore { get; set; }
    public double Gap { get; set; }
    public double PriorityScore { get; set; }
}

public class CoachSkillResultDto
{
    public string Skill { get; set; } = string.Empty;
    public double SkillScore { get; set; }
    public double TargetScore { get; set; }
    public double Gap { get; set; }
    public double ImportanceWeight { get; set; }
    public string? DemonstratedDifficulty { get; set; }
    public string Band { get; set; } = "needs_improvement"; // strength | needs_improvement | critical_gap
    public string? Source { get; set; }
}

public class CoachRoadmapDto
{
    public Guid Id { get; set; }
    public string Skill { get; set; } = string.Empty;
    public double? CurrentScore { get; set; }
    public double TargetScore { get; set; }
    public double Gap { get; set; }
    public double PriorityScore { get; set; }
    public string Kind { get; set; } = "gap";
    public string Priority { get; set; } = "medium";
    public string Status { get; set; } = string.Empty;
    public string? SourceMode { get; set; }
    public string? Explanation { get; set; }
    public List<CoachRoadmapItemDto> Items { get; set; } = new();
}

public class CoachRoadmapItemDto
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Subtopic { get; set; }
    public int SortOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsReassessmentGate { get; set; }
    public double? DrillScore { get; set; }
    public Guid? DrillQuestionSetId { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceTitle { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public List<string> NextTopics { get; set; } = new();
}
