using DomainLayer.Entities;
using DomainLayer.Studio.Enums;

namespace DomainLayer.Studio;

public sealed class InterviewProject : BaseEntity
{
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public InterviewProjectStatus Status { get; set; } = InterviewProjectStatus.Draft;
    public int LatestPlanRevision { get; set; }
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
}

public sealed class AiChatSession : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public AiModelSelectionMode SelectionMode { get; set; } = AiModelSelectionMode.Auto;
    public string? SelectedModelDisplayName { get; set; }
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
}

public sealed class PlanFocusArea : BaseEntity
{
    public Guid InterviewPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int OrderIndex { get; set; }
}

public sealed class PlanApprovalHistory : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid InterviewPlanId { get; set; }
    public int Revision { get; set; }
    public PlanApprovalAction Action { get; set; }
    public Guid ActorId { get; set; }
    public string? Notes { get; set; }
}

public sealed class StudioSettings : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid AppliedPlanId { get; set; }
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
}

public sealed class StudioFocusArea : BaseEntity
{
    public Guid StudioSettingsId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int OrderIndex { get; set; }
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
    /// <summary>SCRUM-372: Job History (QuestionGenerationJob) đã mirror từ run này — regenerate tái sử dụng cùng Id.</summary>
    public Guid? MirroredJobId { get; set; }
}

public sealed class ProjectShare : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string Token { get; set; } = string.Empty;
    public SharePermission Permission { get; set; } = SharePermission.View;
    public DateTime? ExpiresAt { get; set; }
    public Guid CreatedBy { get; set; }
}
