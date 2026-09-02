using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Contracts;

// ── Studio project DTO (Save/Publish linkage)
public sealed record StudioProjectDto(Guid Id, string Name, string? Description, InterviewProjectStatus Status);
public sealed record StudioProjectDetailDto(
    Guid Id,
    Guid OwnerId,
    string Name,
    string? Description,
    InterviewProjectStatus Status,
    int LatestPlanRevision,
    Guid? QuestionSetId = null,
    bool IsPublished = false,
    string? QuestionSetStatus = null);

public sealed record StudioSaveQuestionSetResponseDto(
    Guid QuestionSetId,
    string Status,
    int QuestionCount,
    DateTime SavedAt);

/// <summary>SCRUM-439: Publish từ Studio — chọn interview question ids + time limit (+ recommend).</summary>
public sealed record StudioPublishRequestDto(
    IReadOnlyList<Guid>? InterviewQuestionIds = null,
    int? TimeLimitMinutes = null,
    bool? AutoRecommendEnabled = null,
    double? RecommendationMinScore = null);

public sealed record CreateStudioProjectRequest(string Name, string? Description);
public sealed record UpdateStudioProjectRequest(string Name, string? Description);

public sealed record UpsertJobDescriptionRequest(string Content, JobDescriptionSourceType SourceType, string? OriginalFileName = null);

/// <summary>SCRUM-416: Position map từ JobDescription.Title (extract / HR sửa).</summary>
public sealed record AnalyzeJobDescriptionResponse(
    string? DetectedRole,
    string? DetectedSeniority,
    string? DetectedLanguage,
    string[] Skills,
    string? Position = null,
    string? JobTitle = null,
    string? ExperienceLevel = null,
    IReadOnlyList<string>? Responsibilities = null,
    string? Summary = null);

/// <summary>Job profile gửi RAG recommend — lowercase experienceLevel/detectedLanguage.</summary>
public sealed record JobProfileDto(
    string? JobTitle,
    string? ExperienceLevel,
    string? DetectedRole,
    string? DetectedLanguage,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Responsibilities,
    string? Summary);

public sealed record QuestionDistributionItemDto(string Category, int Percentage, int QuestionCount);

public sealed record StudioFocusAreaItemDto(
    string Name,
    decimal Weight,
    int OrderIndex,
    string? Description = null,
    string? SourceReason = null);

public sealed record RecommendedConfigurationDto(
    int NumberOfQuestions,
    string Difficulty,
    IReadOnlyList<QuestionDistributionItemDto> QuestionDistribution,
    IReadOnlyList<StudioFocusAreaItemDto> FocusAreas,
    IReadOnlyList<string> QuestionStyles,
    IReadOnlyList<string> CodingTaskTypes,
    bool CodingTasksRecommended);

public sealed record RecommendInterviewConfigurationRequestDto(int? NumberOfQuestions = null);

public sealed record RecommendInterviewConfigurationResponseDto(
    JobProfileDto JobProfile,
    RecommendedConfigurationDto RecommendedConfiguration);

public sealed record JobDescriptionContentDto(
    string Content,
    JobDescriptionSourceType SourceType,
    string? OriginalFileName,
    int WordCount,
    int CharacterCount,
    AnalyzeJobDescriptionResponse? Summary,
    string? Position = null);

/// <summary>Kết quả upload JD file: text đã extract + summary auto-detected.</summary>
public sealed record UploadJobDescriptionResponse(
    string Content,
    string? OriginalFileName,
    JobDescriptionSourceType SourceType,
    int WordCount,
    int CharacterCount,
    AnalyzeJobDescriptionResponse Summary);

/// <summary>SCRUM-416: HR xác nhận / sửa vị trí đã extract từ JD.</summary>
public sealed record UpdateJobDescriptionPositionRequest(string Position);

/// <summary>
/// SCRUM-417: HR xác nhận Position + Level (+ Role / Skills) trước generate plan.
/// Skills null = không đổi DetectedSkillsJson; list (kể cả rỗng) = ghi đè sau normalize.
/// </summary>
public sealed record UpdateJobDescriptionMetadataRequest(
    string Position,
    string DetectedSeniority,
    string? DetectedRole = null,
    IReadOnlyList<string>? Skills = null);

public sealed record PlanSummaryDto(Guid Id, int Revision, string Title, InterviewPlanStatus Status, int TotalQuestions);

/// <summary>SCRUM-388: Kết quả refine chat — plan + settings đã sync + citations.</summary>
public sealed record PlanRefineResultDto(
    Guid Id,
    int Revision,
    string Title,
    InterviewPlanStatus Status,
    int TotalQuestions,
    StudioSettingsDto Settings,
    IReadOnlyList<string> ChangedFields,
    IReadOnlyList<string> CitationSourceFiles,
    string AssistantMessage);

public sealed record PlanApprovalHistoryDto(Guid Id, int Revision, PlanApprovalAction Action, Guid ActorId, DateTime CreatedAt, string? Notes);
public sealed record RejectPlanRequest(string? Notes);

/// <summary>SCRUM-393: đổi tiêu đề / tên công việc trên plan Studio.</summary>
public sealed record RenamePlanTitleRequest(string Title);
public sealed record PlanDifficultyMixDto(int Easy, int Medium, int Hard);

/// <summary>SCRUM-420: Một mục provenance trên coverage/focus/outline.</summary>
public sealed record PlanProvenanceItemDto(
    string Origin,
    string? SourceFile,
    int? ChunkIndex,
    string? Excerpt,
    IReadOnlyList<string> UsedFor,
    string? Reason);

public sealed record PlanProvenanceBlockDto(
    string PrimaryOrigin,
    IReadOnlyList<PlanProvenanceItemDto> Items);

public sealed record PlanCoverageItemDto(
    string Skill,
    int QuestionCount,
    IReadOnlyList<string> FocusAreas,
    IReadOnlyList<string> SourceFiles,
    PlanProvenanceBlockDto? Provenance = null);

public sealed record PlanFocusAreaItemDto(
    string Name,
    decimal Weight,
    int OrderIndex,
    IReadOnlyList<string> SourceFiles,
    string? PrimaryOrigin = null,
    PlanProvenanceBlockDto? Provenance = null);
public sealed record PlanSectionItemDto(Guid Id, string Name, string? Description, int OrderIndex, int NumberOfQuestions, QuestionDifficulty Difficulty, int EstimatedMinutes);

/// <summary>Live preview slot — 1 item trong recommendedQuestionOutline (HR chỉnh được trước Generate).</summary>
public sealed record PlanOutlineItemDto(
    int Order,
    string Type,
    string Difficulty,
    string Skill,
    string FocusArea,
    string Goal,
    /// <summary>Text = lý thuyết | Code = coding task.</summary>
    string AnswerMethod = "Text",
    /// <summary>SCRUM-426: nguồn đã khóa (JD + Admin) trên slot.</summary>
    IReadOnlyList<StudioQuestionCitationDto>? Citations = null);

/// <summary>SCRUM-419: Nguồn plan kèm scope HR / SYSTEM / JD / LLM.</summary>
public sealed record PlanSourceUsedDto(string Name, string? Scope);

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
    Guid ConcurrencyVersion,
    IReadOnlyList<PlanSourceUsedDto>? SourceDetails = null,
    IReadOnlyList<PlanCoverageItemDto>? Coverage = null,
    bool IsSettingsStale = false,
    IReadOnlyList<PlanOutlineItemDto>? OutlineItems = null,
    /// <summary>RAG | StudioSettingsPatch | … — FE gate Live Preview sau Apply.</summary>
    string? GeneratedByModelName = null);

public sealed record ApprovePlanRequest(int Revision, Guid ConcurrencyVersion, string? Notes);

public sealed record GenerateQuestionsRequest(Guid PlanId, bool ReplaceExisting, bool IncludeSampleAnswers, bool IncludeScoringRubric);
public sealed record RegenerateQuestionRequest(
    bool IncludeSampleAnswers,
    bool IncludeScoringRubric,
    /// <summary>SCRUM-428: lưu ý HR khi regen (optional, max 1000).</summary>
    string? Instruction = null);
public sealed record GenerationRunDto(
    Guid Id,
    Guid PlanId,
    QuestionGenerationStatus Status,
    int RequestedQuestionCount,
    int GeneratedQuestionCount,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorCode,
    string? ErrorMessage,
    /// <summary>SCRUM-429: regen nền — id câu đang regen (null = generate full).</summary>
    Guid? TargetQuestionId = null);

/// <summary>SCRUM-390: Citation RAG gắn từng câu hỏi. SCRUM-421: origin/usedFor/reason.</summary>
public sealed record StudioQuestionCitationDto(
    string SourceFile,
    int? ChunkIndex = null,
    string? Excerpt = null,
    string? KnowledgeBase = null,
    string? Origin = null,
    IReadOnlyList<string>? UsedFor = null,
    string? Reason = null);

public sealed record StudioQuestionDto(
    Guid Id,
    string Content,
    QuestionDifficulty Difficulty,
    QuestionType Type,
    int OrderIndex,
    string? ExpectedAnswer = null,
    string? ScoringRubric = null,
    IReadOnlyList<StudioQuestionCitationDto>? Citations = null,
    string? CodeTemplateType = null,
    string? CodeSnippet = null,
    // SCRUM-396: gợi ý text hình ảnh cho HR + SAS URL ảnh đính kèm
    string? ImageHint = null,
    string? AttachedImageUrl = null,
    // SCRUM-400: Text | Code
    string? AnswerMethod = null,
    // SCRUM-418: RubricV1 JSON document
    string? RubricJson = null,
    // SCRUM-421: waterfall provenance tóm tắt + cảnh báo thiếu Admin KB
    PlanProvenanceBlockDto? SourceProvenance = null,
    bool MissingAdminWarning = false,
    /// <summary>SCRUM-427: lý do hỏi — copy từ outline.goal khi gen from plan.</summary>
    string? Rationale = null,
    /// <summary>SCRUM-436: skill/tech từ TagsJson (outline) — badge UI.</summary>
    string? Skill = null,
    string? FocusArea = null);
public sealed record StudioQuestionListRequest(Guid? PlanId, Guid? SectionId, QuestionDifficulty? Difficulty, QuestionType? Type, string? Search, int Page = 1, int PageSize = 20);
public sealed record StudioQuestionListResponse(int Page, int PageSize, int Total, IReadOnlyList<StudioQuestionDto> Items);
public sealed record UpdateQuestionRequest(
    string Content,
    QuestionDifficulty Difficulty,
    QuestionType Type,
    int EstimatedMinutes,
    string? ExpectedAnswer,
    string? ScoringRubric,
    /// <summary>SCRUM-418: RubricV1 JSON — ưu tiên hơn ScoringRubric text.</summary>
    string? RubricJson = null);

public sealed record StudioReadinessDto(bool HasJobDescription, bool HasSelectedDocument, bool HasAwaitingApprovalPlan, bool HasApprovedPlan, bool CanGenerateQuestions);
public sealed record UpdateStudioSettingsRequest(
    int InterviewLengthMinutes,
    int NumberOfQuestions,
    QuestionDifficulty Difficulty,
    bool IncludeSampleAnswers,
    bool IncludeScoringRubric,
    /// <summary>Deprecated — FE không còn gửi; BE luôn ghi default Professional.</summary>
    string? QuestionTone = null,
    /// <summary>Deprecated — FE không còn gửi; BE luôn ghi default StructuredInterviewKit.</summary>
    string? OutputFormat = null,
    IReadOnlyList<string>? QuestionTypes = null,
    string? ContentMode = null,
    IReadOnlyList<string>? EnabledCodeTemplates = null,
    // Vietnamese | English — ngôn ngữ câu hỏi đầu ra
    string? Language = null,
    string? OutputLanguage = null,
    IReadOnlyList<QuestionDistributionItemDto>? QuestionDistribution = null,
    IReadOnlyList<StudioFocusAreaItemDto>? FocusAreas = null,
    IReadOnlyList<string>? QuestionStyles = null);
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
    IReadOnlyList<string> QuestionTypes,
    // Vietnamese | English
    string Language = "Vietnamese",
    string ContentMode = "Mixed",
    IReadOnlyList<string>? EnabledCodeTemplates = null,
    IReadOnlyList<QuestionDistributionItemDto>? QuestionDistribution = null,
    IReadOnlyList<StudioFocusAreaItemDto>? FocusAreas = null,
    IReadOnlyList<string>? QuestionStyles = null,
    RecommendedConfigurationDto? RecommendedConfiguration = null,
    DateTime? RecommendedGeneratedAt = null);
public sealed record ApplyPlanSettingsRequest(
    int NumberOfQuestions,
    QuestionDifficulty Difficulty,
    int InterviewLengthMinutes,
    IReadOnlyList<string> QuestionTypes,
    IReadOnlyList<QuestionDistributionItemDto>? QuestionDistribution = null,
    IReadOnlyList<StudioFocusAreaItemDto>? FocusAreas = null,
    IReadOnlyList<string>? QuestionStyles = null,
    IReadOnlyList<string>? CodingTaskTypes = null,
    /// <summary>Khi có: ghi đúng outline HR (preview); NumberOfQuestions nên = Count.</summary>
    IReadOnlyList<PlanOutlineItemDto>? OutlineItems = null);

public sealed record CreateShareLinkRequest(SharePermission Permission, DateTime? ExpiresAt);
public sealed record ShareLinkDto(Guid Id, string Token, SharePermission Permission, DateTime? ExpiresAt, bool IsActive);
public sealed record SharedProjectDto(Guid ProjectId, string ProjectName, string? Description, InterviewProjectStatus Status);

public sealed record UploadStudioDocumentRequest(
    string FileName,
    string ContentType,
    byte[] Content,
    bool IsSelected,
    /// <summary>SCRUM-442: Policy | InternalStack | Rubric | RolePack — bắt buộc khi upload từ Studio.</summary>
    string? DocumentType = null);

/// <summary>SCRUM-373: gắn Knowledge Document đã có (KB HR) vào Studio project.</summary>
public sealed record AttachStudioDocumentsRequest(IReadOnlyList<Guid> KnowledgeDocumentIds, bool IsSelected = true);

/// <summary>Doc trong Knowledge Base chưa gắn vào project (hoặc đã gắn — flag Attached).</summary>
public sealed record StudioLibraryDocumentDto(
    Guid KnowledgeDocumentId,
    string FileName,
    string Status,
    int? ChunkCount,
    DateTime CreatedAt,
    bool AlreadyAttached,
    /// <summary>HR | SYSTEM — SCRUM-419</summary>
    string Scope = "HR",
    /// <summary>SCRUM-442: Policy | … | Unclassified</summary>
    string DocumentType = "Unclassified");

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
    bool IsLibraryLink = false,
    /// <summary>HR | SYSTEM — SCRUM-419</summary>
    string Scope = "HR",
    /// <summary>SCRUM-442</summary>
    string DocumentType = "Unclassified");

/// <summary>SCRUM-443: gợi ý gắn KB theo JD.</summary>
public sealed record StudioKnowledgeSuggestionDto(
    Guid KnowledgeDocumentId,
    string FileName,
    string DocumentType,
    double MaxScore,
    int HitCount,
    string? TopExcerpt);

public sealed record StudioRetrievePreviewRequest(Guid KnowledgeDocumentId);

public sealed record StudioRetrievePreviewChunkDto(
    int ChunkIndex,
    string Content,
    double Score,
    string? FileName);

public sealed record StudioRetrievePreviewDto(
    Guid KnowledgeDocumentId,
    IReadOnlyList<StudioRetrievePreviewChunkDto> Chunks);

public sealed record SseEvent(string Event, string Data);
public sealed record ChatSessionDto(Guid SessionId, Guid ProjectId, Guid UserId, DateTime CreatedAt);
public sealed record ChatMessageDto(Guid Id, Guid SessionId, AiChatMessageRole Role, string Content, AiMessageStatus Status, DateTime CreatedAt);
