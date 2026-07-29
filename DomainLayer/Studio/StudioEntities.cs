using DomainLayer.Entities;
using DomainLayer.Studio.Enums;

namespace DomainLayer.Studio;

/// <summary>Project Studio — root pipeline generate. Soft-delete qua IsActive; FK con Restrict tránh hard-delete nuốt data marketplace.</summary>
public sealed class InterviewProject : BaseEntity
{
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public InterviewProjectStatus Status { get; set; } = InterviewProjectStatus.Draft;
    public int LatestPlanRevision { get; set; }

    public User? Owner { get; set; }
    public JobDescription? JobDescription { get; set; }
    public StudioSettings? Settings { get; set; }
    public ICollection<StudioKnowledgeDocument> KnowledgeDocuments { get; set; } = new List<StudioKnowledgeDocument>();
    public ICollection<AiChatSession> ChatSessions { get; set; } = new List<AiChatSession>();
    public ICollection<InterviewPlan> Plans { get; set; } = new List<InterviewPlan>();
    public ICollection<QuestionGenerationRun> GenerationRuns { get; set; } = new List<QuestionGenerationRun>();
    public ICollection<InterviewQuestion> Questions { get; set; } = new List<InterviewQuestion>();
    public ICollection<ProjectShare> Shares { get; set; } = new List<ProjectShare>();
    public ICollection<PlanApprovalHistory> ApprovalHistories { get; set; } = new List<PlanApprovalHistory>();
}

public sealed class JobDescription : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public JobDescriptionSourceType SourceType { get; set; } = JobDescriptionSourceType.PastedText;
    public string? OriginalFileName { get; set; }
    public string? DetectedRole { get; set; }
    public string? DetectedSeniority { get; set; }
    public string? DetectedLanguage { get; set; }
    public string? DetectedSkillsJson { get; set; }
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }

    public InterviewProject? Project { get; set; }
}

public sealed class StudioKnowledgeDocument : BaseEntity
{
    public Guid ProjectId { get; set; }
    /// <summary>Link sang knowledge_documents để ingest RAG giống luồng HR cũ.</summary>
    public Guid? KnowledgeDocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string? ExtractedText { get; set; }
    public bool IsSelected { get; set; }
    public DocumentProcessingStatus ProcessingStatus { get; set; } = DocumentProcessingStatus.Pending;
    public string? ProcessingError { get; set; }

    public InterviewProject? Project { get; set; }
    public KnowledgeDocument? KnowledgeDocument { get; set; }
}

public sealed class AiChatSession : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public AiModelSelectionMode SelectionMode { get; set; } = AiModelSelectionMode.Auto;
    public string? SelectedModelDisplayName { get; set; }

    public InterviewProject? Project { get; set; }
    public User? User { get; set; }
    public ICollection<AiChatMessage> Messages { get; set; } = new List<AiChatMessage>();
    public ICollection<InterviewPlan> Plans { get; set; } = new List<InterviewPlan>();
}

public sealed class AiChatMessage : BaseEntity
{
    public Guid SessionId { get; set; }
    public AiChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public AiMessageStatus Status { get; set; } = AiMessageStatus.Pending;
    public Guid? RelatedPlanId { get; set; }
    public int? RelatedPlanRevision { get; set; }
    public string? ResolvedModelName { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime? CompletedAt { get; set; }

    public AiChatSession? Session { get; set; }
    public InterviewPlan? RelatedPlan { get; set; }
}

public sealed class InterviewPlan : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid SessionId { get; set; }
    public int Revision { get; set; }
    public InterviewPlanStatus Status { get; set; } = InterviewPlanStatus.Draft;
    public string Title { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public int InterviewLengthMinutes { get; set; }
    public string SeniorityLevel { get; set; } = "Mid";
    public string Language { get; set; } = "en";
    public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;
    public string QuestionTone { get; set; } = "Professional";
    public bool IncludeSampleAnswers { get; set; }
    public bool IncludeScoringRubric { get; set; }
    public string OutputFormat { get; set; } = "Markdown";
    public string? GeneratedByModelName { get; set; }
    /// <summary>SCRUM-367: raw plan JSON từ RAG — dùng lại khi generate questions.</summary>
    public string? SourcePlanJson { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    public InterviewProject? Project { get; set; }
    public AiChatSession? Session { get; set; }
    public User? ApprovedByUser { get; set; }
    public ICollection<PlanSection> Sections { get; set; } = new List<PlanSection>();
    public ICollection<PlanFocusArea> FocusAreas { get; set; } = new List<PlanFocusArea>();
    public ICollection<PlanApprovalHistory> ApprovalHistories { get; set; } = new List<PlanApprovalHistory>();
    public ICollection<QuestionGenerationRun> GenerationRuns { get; set; } = new List<QuestionGenerationRun>();
    public ICollection<InterviewQuestion> Questions { get; set; } = new List<InterviewQuestion>();
    public ICollection<StudioSettings> AppliedInSettings { get; set; } = new List<StudioSettings>();
}

public sealed class PlanSection : BaseEntity
{
    public Guid InterviewPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int OrderIndex { get; set; }
    public int NumberOfQuestions { get; set; }
    public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;
    public int EstimatedMinutes { get; set; }

    public InterviewPlan? InterviewPlan { get; set; }
    public ICollection<InterviewQuestion> Questions { get; set; } = new List<InterviewQuestion>();
}

public sealed class PlanFocusArea : BaseEntity
{
    public Guid InterviewPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int OrderIndex { get; set; }

    public InterviewPlan? InterviewPlan { get; set; }
}

public sealed class PlanApprovalHistory : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid InterviewPlanId { get; set; }
    public int Revision { get; set; }
    public PlanApprovalAction Action { get; set; }
    public Guid ActorId { get; set; }
    public string? Notes { get; set; }

    public InterviewProject? Project { get; set; }
    public InterviewPlan? InterviewPlan { get; set; }
    public User? Actor { get; set; }
}

public sealed class StudioSettings : BaseEntity
{
    public Guid ProjectId { get; set; }
    /// <summary>Plan đã approve áp dụng generate — null nếu settings tạo trước khi có plan (FK optional).</summary>
    public Guid? AppliedPlanId { get; set; }
    public int InterviewLengthMinutes { get; set; }
    public int NumberOfQuestions { get; set; }
    public string SeniorityLevel { get; set; } = "Mid";
    public string Language { get; set; } = "en";
    public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;
    public string QuestionTone { get; set; } = "Professional";
    public bool IncludeSampleAnswers { get; set; }
    public bool IncludeScoringRubric { get; set; }
    public string OutputFormat { get; set; } = "Markdown";
    /// <summary>SCRUM-370: JSON array loại câu — ["technical","system_design","problem_solving","behavioral"].</summary>
    public string? QuestionTypesJson { get; set; }
    /// <summary>Question content mode: TheoryOnly | CodeOnly | Mixed.</summary>
    public string ContentMode { get; set; } = "Mixed";
    /// <summary>JSON array code template ids enabled for generation/builder.</summary>
    public string? CodeTemplatesJson { get; set; }

    public InterviewProject? Project { get; set; }
    public InterviewPlan? AppliedPlan { get; set; }
    public ICollection<StudioFocusArea> FocusAreas { get; set; } = new List<StudioFocusArea>();
}

public sealed class StudioFocusArea : BaseEntity
{
    public Guid StudioSettingsId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int OrderIndex { get; set; }

    public StudioSettings? StudioSettings { get; set; }
}

public sealed class InterviewQuestion : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid InterviewPlanId { get; set; }
    public Guid PlanSectionId { get; set; }
    public Guid GenerationRunId { get; set; }
    public string Content { get; set; } = string.Empty;
    public QuestionType Type { get; set; } = QuestionType.Technical;
    public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;
    public string? ExpectedAnswer { get; set; }
    public string? ScoringRubric { get; set; }
    public int EstimatedMinutes { get; set; }
    public int OrderIndex { get; set; }
    public string? TagsJson { get; set; }
    public string? GeneratedByModelName { get; set; }

    public InterviewProject? Project { get; set; }
    public InterviewPlan? InterviewPlan { get; set; }
    public PlanSection? PlanSection { get; set; }
    public QuestionGenerationRun? GenerationRun { get; set; }
}

public sealed class QuestionGenerationRun : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid InterviewPlanId { get; set; }
    public Guid RequestedBy { get; set; }
    public QuestionGenerationStatus Status { get; set; } = QuestionGenerationStatus.Pending;
    public int RequestedQuestionCount { get; set; }
    public int GeneratedQuestionCount { get; set; }
    public bool ReplaceExisting { get; set; }
    public bool IncludeSampleAnswers { get; set; }
    public bool IncludeScoringRubric { get; set; }
    public QuestionGeneratorType GeneratorType { get; set; } = QuestionGeneratorType.Mock;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public InterviewProject? Project { get; set; }
    public InterviewPlan? InterviewPlan { get; set; }
    public User? RequestedByUser { get; set; }
    public ICollection<InterviewQuestion> Questions { get; set; } = new List<InterviewQuestion>();
}

public sealed class ProjectShare : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string Token { get; set; } = string.Empty;
    public SharePermission Permission { get; set; } = SharePermission.View;
    public DateTime? ExpiresAt { get; set; }
    public Guid CreatedBy { get; set; }

    public InterviewProject? Project { get; set; }
    public User? CreatedByUser { get; set; }
}
