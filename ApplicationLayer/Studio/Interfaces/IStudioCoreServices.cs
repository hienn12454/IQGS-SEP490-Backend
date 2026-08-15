using ApplicationLayer.Studio.Contracts;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Interfaces;

public interface IInterviewProjectService
{
    Task<StudioProjectDto> CreateAsync(Guid userId, CreateStudioProjectRequest request, CancellationToken ct);
    Task<IReadOnlyList<StudioProjectDto>> ListByOwnerAsync(Guid userId, CancellationToken ct);
    Task<StudioProjectDetailDto> GetAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task<StudioProjectDetailDto> UpdateAsync(Guid projectId, Guid userId, UpdateStudioProjectRequest request, CancellationToken ct);
    Task DeleteAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task<InterviewProject> EnsureProjectAccessAsync(Guid projectId, Guid userId, bool requireEdit, CancellationToken ct);
    /// <summary>Snapshot InterviewQuestions → question_sets (private DRAFT). UX: Save.</summary>
    Task<StudioSaveQuestionSetResponseDto> SaveQuestionSetAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task PublishFromProjectAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task<ApplicationLayer.DTOs.QuestionSet.QuestionSetActionResponseDto> UnpublishFromProjectAsync(Guid projectId, Guid userId, CancellationToken ct);
}

public interface IJobDescriptionService
{
    Task UpsertAsync(Guid projectId, Guid userId, UpsertJobDescriptionRequest request, CancellationToken ct);
    Task<AnalyzeJobDescriptionResponse> AnalyzeAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task<JobDescriptionContentDto?> GetContentAsync(Guid projectId, Guid userId, CancellationToken ct);
}

/// <summary>Upload JD file (PDF/DOCX/TXT/ảnh) → extract/OCR → lưu → analyze summary.</summary>
public interface IStudioJobDescriptionUploadService
{
    Task<UploadJobDescriptionResponse> UploadAsync(
        Guid projectId,
        Guid userId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken ct);
}

/// <summary>Phân tích JD → Role/Seniority/Language/Skills. Mock hiện tại; có thể thay bằng AI provider.</summary>
public interface IJobDescriptionAnalyzer
{
    Task<AnalyzeJobDescriptionResponse> AnalyzeAsync(string content, CancellationToken cancellationToken);
}

public interface IStudioKnowledgeDocumentService
{
    Task<StudioDocumentDto> UploadAsync(Guid projectId, Guid userId, UploadStudioDocumentRequest request, CancellationToken ct);
    Task<IReadOnlyList<StudioDocumentDto>> ListAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task<StudioDocumentDto> SetSelectionAsync(Guid projectId, Guid documentId, Guid userId, bool isSelected, CancellationToken ct);
    Task DeleteAsync(Guid projectId, Guid documentId, Guid userId, CancellationToken ct);
    Task<StudioDocumentDto> ReingestAsync(Guid projectId, Guid documentId, Guid userId, CancellationToken ct);

    /// <summary>SCRUM-373: list Knowledge Documents HR (COMPLETED) để chọn gắn vào project.</summary>
    Task<IReadOnlyList<StudioLibraryDocumentDto>> ListLibraryAsync(Guid projectId, Guid userId, CancellationToken ct);

    /// <summary>SCRUM-373: gắn 1+ KnowledgeDocumentId đã upload ở KB vào Studio project (không upload lại).</summary>
    Task<IReadOnlyList<StudioDocumentDto>> AttachFromLibraryAsync(
        Guid projectId, Guid userId, AttachStudioDocumentsRequest request, CancellationToken ct);
}

public interface IInterviewPlanService
{
    Task<IReadOnlyList<PlanSummaryDto>> ListAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task<PlanDetailDto?> GetCurrentAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task<PlanDetailDto> GetDetailAsync(Guid projectId, Guid planId, Guid userId, CancellationToken ct);
    Task<PlanSummaryDto> GenerateInitialAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task<PlanRefineResultDto> RefineAsync(Guid projectId, Guid planId, Guid userId, string instruction, CancellationToken ct);
    Task<PlanSummaryDto> ApplySettingsAsync(Guid projectId, Guid planId, Guid userId, ApplyPlanSettingsRequest request, CancellationToken ct);
    Task<PlanSummaryDto> SubmitForApprovalAsync(Guid projectId, Guid planId, Guid userId, CancellationToken ct);
    Task<PlanSummaryDto> RejectAsync(Guid projectId, Guid planId, Guid userId, RejectPlanRequest request, CancellationToken ct);
    Task<PlanSummaryDto> ApproveAsync(Guid projectId, Guid planId, Guid userId, ApprovePlanRequest request, CancellationToken ct);
    /// <summary>SCRUM-393: đổi Title plan (tên công việc hiển thị) — sync roleTitle trong SourcePlanJson nếu có.</summary>
    Task<PlanSummaryDto> RenameTitleAsync(Guid projectId, Guid planId, Guid userId, RenamePlanTitleRequest request, CancellationToken ct);
    Task<IReadOnlyList<PlanApprovalHistoryDto>> GetApprovalHistoryAsync(Guid projectId, Guid planId, Guid userId, CancellationToken ct);
}

public interface IQuestionGenerationService
{
    /// <summary>SCRUM-371: enqueue RAG async — trả GenerationRun ngay; câu hỏi đến qua callback.</summary>
    Task<GenerationRunDto> GenerateAsync(Guid projectId, Guid userId, GenerateQuestionsRequest request, CancellationToken ct);

    /// <summary>RAG PATCH generation-result với jobId = QuestionGenerationRun.Id (phase QUESTIONS).</summary>
    Task<bool> TryApplyRagCallbackAsync(Guid runId, ApplicationLayer.DTOs.QuestionGeneration.QuestionGenerationCallbackDto dto, CancellationToken ct = default);

    Task<StudioQuestionListResponse> ListQuestionsAsync(Guid projectId, Guid userId, StudioQuestionListRequest request, CancellationToken ct);
    Task<StudioQuestionDto> UpdateQuestionAsync(Guid projectId, Guid questionId, Guid userId, UpdateQuestionRequest request, CancellationToken ct);
    Task DeleteQuestionAsync(Guid projectId, Guid questionId, Guid userId, CancellationToken ct);
    Task<StudioQuestionDto> RegenerateQuestionAsync(Guid projectId, Guid questionId, Guid userId, RegenerateQuestionRequest request, CancellationToken ct);
    /// <summary>SCRUM-396: upload ảnh đính kèm câu hỏi lên Azure Blob.</summary>
    Task<StudioQuestionDto> UploadQuestionImageAsync(Guid projectId, Guid questionId, Guid userId, Stream fileStream, string fileName, string contentType, long fileLength, CancellationToken ct);
    /// <summary>SCRUM-396: xóa ảnh đính kèm câu hỏi.</summary>
    Task<StudioQuestionDto> DeleteQuestionImageAsync(Guid projectId, Guid questionId, Guid userId, CancellationToken ct);
    Task<IReadOnlyList<GenerationRunDto>> ListRunsAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task<GenerationRunDto> GetRunAsync(Guid projectId, Guid runId, Guid userId, CancellationToken ct);
}

public interface IAiChatService
{
    IAsyncEnumerable<SseEvent> StreamMessageAsync(Guid projectId, Guid userId, string message, CancellationToken ct);
    Task<ChatSessionDto> GetSessionAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid projectId, Guid userId, CancellationToken ct);
    /// <summary>SCRUM-376: Ghi cặp user/assistant vào transcript (không qua SSE mock).</summary>
    Task AppendUserAndAssistantAsync(
        Guid projectId,
        Guid userId,
        string userContent,
        string assistantContent,
        Guid? relatedPlanId,
        int? relatedPlanRevision,
        CancellationToken ct);
}

public interface IStudioSettingsService
{
    Task<StudioSettingsDto> GetAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task<StudioSettingsDto> UpdateAsync(Guid projectId, Guid userId, UpdateStudioSettingsRequest request, CancellationToken ct);
}

public interface IStudioShareService
{
    Task<ShareLinkDto> CreateAsync(Guid projectId, Guid userId, CreateShareLinkRequest request, CancellationToken ct);
    Task RevokeAsync(Guid projectId, Guid shareId, Guid userId, CancellationToken ct);
    Task<SharedProjectDto> ResolveAsync(string token, CancellationToken ct);
}

public interface IStudioMockAiService
{
    (int TotalQuestions, int InterviewLengthMinutes, string Title) BuildInitialPlan(string? detectedRole, string? seniority);
    (int TotalQuestions, int InterviewLengthMinutes, QuestionDifficulty Difficulty, string Note) ApplyRefineRule(int totalQuestions, int minutes, QuestionDifficulty difficulty, string instruction);
    string GenerateQuestionText(string sectionName, int order);
}

public interface IDocumentTextExtractor
{
    bool CanHandle(string contentType, string fileName);
    Task<string> ExtractTextAsync(byte[] content, CancellationToken ct);
}

public interface IDocumentTextExtractorFactory
{
    IDocumentTextExtractor Resolve(string contentType, string fileName);
}
