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
    /// <summary>English | Vietnamese — null nếu user chưa chọn ở bước CV.</summary>
    public string? OutputLanguage { get; set; }
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
    /// <summary>SCRUM-494: catalog Role Family cho dropdown Vị trí mục tiêu (optgroup theo GroupName).</summary>
    public List<CoachRoleFamilyOptionDto> AvailableRoleFamilies { get; set; } = new();
    public List<string> DetectedSkills { get; set; } = new();
}

/// <summary>SCRUM-494: một role family trong catalog dropdown.</summary>
public class CoachRoleFamilyOptionDto
{
    public string FamilyKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? GroupName { get; set; }
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

/// <summary>Ngôn ngữ đầu ra Coach, chọn ở bước CV.</summary>
public class UpdateCoachOutputLanguageDto
{
    [Required]
    [MaxLength(20)]
    public string OutputLanguage { get; set; } = string.Empty;
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

/// <summary>SCRUM-463: candidate chỉnh danh sách công nghệ trên màn Phân tích CV.</summary>
public class UpdateCoachSkillsDto
{
    [Required]
    [MinLength(1)]
    [MaxLength(40)]
    public List<string> Skills { get; set; } = new();
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
    /// <summary>SCRUM-509: tiêu chí level có cấu trúc — FE hiện Đạt/Chưa đạt, không dump công thức.</summary>
    public CoachLevelCriteriaDto? LevelCriteria { get; set; }
    public double? CoverageRatio { get; set; }
    /// <summary>SCRUM-457: Target Readiness — không dùng AchievedLevel làm headline seniority.</summary>
    public string? ResolutionMode { get; set; }
    public string? TargetLevel { get; set; }
    public double? TargetThreshold { get; set; }
    public double? ReadinessPercent { get; set; }
    public string? EstimatedBand { get; set; }
    public string? TargetReadinessStatus { get; set; }
    public List<CoachSkillGapDto> SkillGaps { get; set; } = new();
    /// <summary>SCRUM-461: level liền kề gợi ý khi READY (Fresher→Junior→Middle→Senior).</summary>
    public string? SuggestedNextLevel { get; set; }
    /// <summary>true nếu có thể promote (framework exact hoặc adaptive).</summary>
    public bool SuggestedNextLevelAvailable { get; set; }
    public string? SuggestedNextLevelMessage { get; set; }
}

public class CoachSkillGapDto
{
    public string Skill { get; set; } = string.Empty;
    public double CurrentScore { get; set; }
    public double TargetScore { get; set; }
    public double Gap { get; set; }
    public double PriorityScore { get; set; }
}

/// <summary>SCRUM-509: 4 tiêu chí suy level — FE hiện nhãn người dùng + Đạt/Chưa đạt.</summary>
public class CoachLevelCriteriaDto
{
    public double Overall { get; set; }
    public double OverallThreshold { get; set; }
    public double TargetMetRatio { get; set; }
    public double TargetMetThreshold { get; set; }
    public double RequiredRatio { get; set; }
    public double RequiredThreshold { get; set; }
    public double HardRatio { get; set; }
    public double HardThreshold { get; set; }
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
    /// <summary>true = skill CV chỉ được hỏi nhanh trong bài chẩn đoán (không tính vào level).</summary>
    public bool IsQuickCheck { get; set; }
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
    /// <summary>system = có retrieve folder coach-roadmap; inferred = suy luận từ node/framework.</summary>
    public string? KbSource { get; set; }
    /// <summary>SCRUM-462: null = chưa Accept; có giá trị = đã chấp nhận lộ trình.</summary>
    public DateTime? AcceptedAt { get; set; }
    /// <summary>cv | outsideCv — skill khớp CV hay pad từ framework.</summary>
    public string? SkillSource { get; set; }
    /// <summary>Lý do gợi ý khi skillSource = outsideCv.</summary>
    public string? OutsideCvReason { get; set; }
    /// <summary>SCRUM-484: thứ tự luyện skill (0 = trước).</summary>
    public int DisplayOrder { get; set; }
    /// <summary>SCRUM-488: ngưỡng pass drill (điểm phải &gt; giá trị này) — từ Admin policy.</summary>
    public double DrillPassScoreExclusiveMin { get; set; } = 70;
    /// <summary>SCRUM-506: screening = tín hiệu 1 câu, cần kiểm tra thêm.</summary>
    public string? Confidence { get; set; }
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
    /// <summary>SCRUM-462: candidate chọn học topic này; gate luôn true.</summary>
    public bool IsIncluded { get; set; } = true;
    /// <summary>Lý do ngắn cho topic (RAG/node/fallback).</summary>
    public string? TopicReason { get; set; }
    public double? DrillScore { get; set; }
    public Guid? DrillQuestionSetId { get; set; }
    /// <summary>SCRUM-484: session luyện đã nộp — FE deep-link xem lại feedback.</summary>
    public Guid? DrillSessionId { get; set; }
    /// <summary>SCRUM-489: mọi phiên COMPLETED trên cùng DrillQuestionSetId (cũ → mới).</summary>
    public List<CoachDrillAttemptDto> DrillAttempts { get; set; } = new();
    public string? SourceUrl { get; set; }
    public string? SourceTitle { get; set; }
    /// <summary>SCRUM-486: KB doc gắn node — FE mở viewer trong app.</summary>
    public Guid? KnowledgeDocumentId { get; set; }
    /// <summary>SCRUM-486: true khi doc AllowCandidateView và đã gắn node.</summary>
    public bool CanViewSource { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public List<string> NextTopics { get; set; } = new();
}

/// <summary>SCRUM-489: một lần nộp drill (COMPLETED) trên topic — FE link xem lại feedback.</summary>
public class CoachDrillAttemptDto
{
    public Guid SessionId { get; set; }
    public double? Score { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>SCRUM-462 / SCRUM-484: toggle topic + reorder skill/topic trên draft Suggested.</summary>
public class UpdateRoadmapDraftDto
{
    public List<UpdateRoadmapDraftItemDto> Items { get; set; } = new();
    /// <summary>SCRUM-484: đổi DisplayOrder các skill draft.</summary>
    public List<UpdateRoadmapDraftRoadmapDto> Roadmaps { get; set; } = new();
}

public class UpdateRoadmapDraftItemDto
{
    public Guid ItemId { get; set; }
    /// <summary>null = không đổi IsIncluded.</summary>
    public bool? IsIncluded { get; set; }
    /// <summary>null = không đổi SortOrder. Gate bỏ qua.</summary>
    public int? SortOrder { get; set; }
}

public class UpdateRoadmapDraftRoadmapDto
{
    public Guid RoadmapId { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>SCRUM-462: chấp nhận toàn bộ roadmap Suggested đang active.</summary>
public class AcceptRoadmapsDto
{
}

/// <summary>SCRUM-506: preview bài sàng lọc skill CV chưa đo.</summary>
public class CoachScreeningPreviewDto
{
    public bool Enabled { get; set; }
    public bool Available { get; set; }
    public int QuestionCount { get; set; }
    public int QuestionsPerSkill { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<string> MeasuredSkills { get; set; } = new();
    public int RemainingUnmeasured { get; set; }
    public string? Message { get; set; }
}

/// <summary>SCRUM-507: tổng kết sau khi mọi lộ trình đã Accept được reassessment.</summary>
public class CoachWrapUpDto
{
    /// <summary>true khi mọi roadmap Accepted đều Completed.</summary>
    public bool Available { get; set; }
    public int CompletedRoadmaps { get; set; }
    public int TotalRoadmaps { get; set; }
    public double? OverallReadiness { get; set; }
    public double? OverallDelta { get; set; }
    public string? AchievedLevel { get; set; }
    public string? TargetReadinessStatus { get; set; }
    public string? SuggestedNextLevel { get; set; }
    public bool SuggestedNextLevelAvailable { get; set; }
    public string? SuggestedNextLevelMessage { get; set; }
    public List<CoachWrapUpSkillDeltaDto> Improved { get; set; } = new();
    public List<CoachWrapUpSkillDto> Strengths { get; set; } = new();
    public List<CoachWrapUpWeakTopicDto> WeakTopics { get; set; } = new();
    public List<CoachWrapUpNextSkillDto> NextSkills { get; set; } = new();
    /// <summary>SCRUM-514: số câu đánh giá lại đạt ngưỡng drill (điểm &gt; ngưỡng, không phải đúng/sai nhị phân).</summary>
    public int AnswerPassedCount { get; set; }
    public int AnswerTotalCount { get; set; }
    public List<CoachWrapUpAnswerDto> Answers { get; set; } = new();
}

/// <summary>SCRUM-514: một câu bài đánh giá lại trên tổng kết.</summary>
public class CoachWrapUpAnswerDto
{
    public string Skill { get; set; } = string.Empty;
    /// <summary>Cắt ~120 ký tự — đủ nhận câu, không dump full đề.</summary>
    public string QuestionPreview { get; set; } = string.Empty;
    public double? Score { get; set; }
    /// <summary>true khi Score &gt; DrillPassScoreExclusiveMin.</summary>
    public bool Passed { get; set; }
}

public class CoachWrapUpSkillDeltaDto
{
    public string Skill { get; set; } = string.Empty;
    public double BaselineScore { get; set; }
    public double CurrentScore { get; set; }
    public double Delta { get; set; }
}

public class CoachWrapUpSkillDto
{
    public string Skill { get; set; } = string.Empty;
    public double CurrentScore { get; set; }
    public double TargetScore { get; set; }
}

public class CoachWrapUpWeakTopicDto
{
    public string Skill { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public double LowestScore { get; set; }
    /// <summary>true nếu sau lần điểm thấp vẫn vượt ngưỡng pass.</summary>
    public bool Overcame { get; set; }
}

public class CoachWrapUpNextSkillDto
{
    public string Skill { get; set; } = string.Empty;
    public double? CurrentScore { get; set; }
    public double TargetScore { get; set; }
    public double Gap { get; set; }
    /// <summary>gap | screening</summary>
    public string Reason { get; set; } = "gap";
}
