using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Contracts;

public sealed record StudioProjectDto(Guid Id, string Name, string? Description, InterviewProjectStatus Status);
public sealed record StudioProjectDetailDto(Guid Id, Guid OwnerId, string Name, string? Description, InterviewProjectStatus Status, int LatestPlanRevision);

public sealed record CreateStudioProjectRequest(string Name, string? Description);
public sealed record UpdateStudioProjectRequest(string Name, string? Description);

public sealed record UpsertJobDescriptionRequest(string Content, JobDescriptionSourceType SourceType, string? OriginalFileName = null);

public sealed record AnalyzeJobDescriptionResponse(string? DetectedRole, string? DetectedSeniority, string? DetectedLanguage, string[] Skills);

public sealed record JobDescriptionContentDto(
    string Content,
    JobDescriptionSourceType SourceType,
    string? OriginalFileName,
    int WordCount,
    int CharacterCount,
    AnalyzeJobDescriptionResponse? Summary);

/// <summary>Kết quả upload JD file: text đã extract + summary auto-detected.</summary>
public sealed record UploadJobDescriptionResponse(
    string Content,
    string? OriginalFileName,
    JobDescriptionSourceType SourceType,
    int WordCount,
    int CharacterCount,
    AnalyzeJobDescriptionResponse Summary);

public sealed record PlanSummaryDto(Guid Id, int Revision, string Title, InterviewPlanStatus Status, int TotalQuestions);
public sealed record PlanApprovalHistoryDto(Guid Id, int Revision, PlanApprovalAction Action, Guid ActorId, DateTime CreatedAt, string? Notes);
public sealed record RejectPlanRequest(string? Notes);
public sealed record PlanDifficultyMixDto(int Easy, int Medium, int Hard);
public sealed record PlanFocusAreaItemDto(
    string Name,
    decimal Weight,
    int OrderIndex,
    IReadOnlyList<string> SourceFiles);
public sealed record PlanSectionItemDto(Guid Id, string Name, string? Description, int OrderIndex, int NumberOfQuestions, QuestionDifficulty Difficulty, int EstimatedMinutes);
public sealed record PlanDetailDto(
    Guid Id,
    Guid ProjectId,
    int Revision,
    string Title,
    InterviewPlanStatus Status,
    int TotalQuestions,
    int InterviewLengthMinutes,
    QuestionDifficulty Difficulty,
    PlanDifficultyMixDto DifficultyMix,
    IReadOnlyList<PlanFocusAreaItemDto> FocusAreas,
    IReadOnlyList<string> SourcesUsed,
    IReadOnlyList<PlanSectionItemDto> EstimatedSections,
    IReadOnlyList<PlanSectionItemDto> Sections,
    Guid ConcurrencyVersion);

public sealed record ApprovePlanRequest(int Revision, Guid ConcurrencyVersion, string? Notes);

public sealed record GenerateQuestionsRequest(Guid PlanId, bool ReplaceExisting, bool IncludeSampleAnswers, bool IncludeScoringRubric);
public sealed record RegenerateQuestionRequest(bool IncludeSampleAnswers, bool IncludeScoringRubric);
public sealed record GenerationRunDto(Guid Id, Guid PlanId, DomainLayer.Studio.Enums.QuestionGenerationStatus Status, int RequestedQuestionCount, int GeneratedQuestionCount, DateTime StartedAt, DateTime? CompletedAt, string? ErrorCode, string? ErrorMessage);

public sealed record StudioQuestionDto(
    Guid Id,
    string Content,
    QuestionDifficulty Difficulty,
    QuestionType Type,
    int OrderIndex,
    string? ExpectedAnswer = null,
    string? ScoringRubric = null);
public sealed record StudioQuestionListRequest(Guid? PlanId, Guid? SectionId, QuestionDifficulty? Difficulty, QuestionType? Type, string? Search, int Page = 1, int PageSize = 20);
public sealed record StudioQuestionListResponse(int Page, int PageSize, int Total, IReadOnlyList<StudioQuestionDto> Items);
public sealed record UpdateQuestionRequest(string Content, QuestionDifficulty Difficulty, QuestionType Type, int EstimatedMinutes, string? ExpectedAnswer, string? ScoringRubric);

public sealed record StudioReadinessDto(bool HasJobDescription, bool HasSelectedDocument, bool HasAwaitingApprovalPlan, bool HasApprovedPlan, bool CanGenerateQuestions);
public sealed record UpdateStudioSettingsRequest(
    int InterviewLengthMinutes,
    int NumberOfQuestions,
    QuestionDifficulty Difficulty,
    string QuestionTone,
    bool IncludeSampleAnswers,
    bool IncludeScoringRubric,
    string OutputFormat,
    IReadOnlyList<string>? QuestionTypes = null);
public sealed record StudioSettingsDto(
    Guid ProjectId,
    Guid? AppliedPlanId,
    int InterviewLengthMinutes,
    int NumberOfQuestions,
    QuestionDifficulty Difficulty,
    string QuestionTone,
    bool IncludeSampleAnswers,
    bool IncludeScoringRubric,
    string OutputFormat,
    StudioReadinessDto Readiness,
    IReadOnlyList<string> QuestionTypes);
public sealed record ApplyPlanSettingsRequest(
    int NumberOfQuestions,
    QuestionDifficulty Difficulty,
    int InterviewLengthMinutes,
    IReadOnlyList<string> QuestionTypes);

public sealed record CreateShareLinkRequest(SharePermission Permission, DateTime? ExpiresAt);
public sealed record ShareLinkDto(Guid Id, string Token, SharePermission Permission, DateTime? ExpiresAt, bool IsActive);
public sealed record SharedProjectDto(Guid ProjectId, string ProjectName, string? Description, InterviewProjectStatus Status);

public sealed record UploadStudioDocumentRequest(string FileName, string ContentType, byte[] Content, bool IsSelected);

/// <summary>SCRUM-373: gắn Knowledge Document đã có (KB HR) vào Studio project.</summary>
public sealed record AttachStudioDocumentsRequest(IReadOnlyList<Guid> KnowledgeDocumentIds, bool IsSelected = true);

/// <summary>Doc trong Knowledge Base chưa gắn vào project (hoặc đã gắn — flag Attached).</summary>
public sealed record StudioLibraryDocumentDto(
    Guid KnowledgeDocumentId,
    string FileName,
    string Status,
    int? ChunkCount,
    DateTime CreatedAt,
    bool AlreadyAttached);

public sealed record StudioDocumentDto(
    Guid Id,
    string FileName,
    string FileType,
    long FileSize,
    bool IsSelected,
    DocumentProcessingStatus Status,
    string? PreviewText,
    Guid? KnowledgeDocumentId = null,
    string? RagStatus = null,
    int? ChunkCount = null,
    string? ProcessingError = null,
    bool IsLibraryLink = false);

public sealed record SseEvent(string Event, string Data);
public sealed record ChatSessionDto(Guid SessionId, Guid ProjectId, Guid UserId, DateTime CreatedAt);
public sealed record ChatMessageDto(Guid Id, Guid SessionId, AiChatMessageRole Role, string Content, AiMessageStatus Status, DateTime CreatedAt);
