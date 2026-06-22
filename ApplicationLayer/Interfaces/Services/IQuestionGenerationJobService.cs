using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.DTOs.QuestionGeneration;

namespace ApplicationLayer.Interfaces.Services;

public interface IQuestionGenerationJobService
{
    Task<CreatePlanJobResponseDto> CreatePlanJobAsync(Guid ownerId, CreatePlanJobRequestDto dto, CancellationToken ct = default);
    Task<CreatePlanJobResponseDto> CreatePlanJobFromUploadAsync(
        Guid ownerId,
        string? jobDescription,
        string? hrNote,
        Stream? fileStream,
        string? fileName,
        long fileSize,
        int numberOfQuestions,
        string difficulty,
        List<string> questionTypes,
        List<string> skills,
        CancellationToken ct = default);
    Task<PagedResultDto<QuestionGenerationJobListItemDto>> ListJobsAsync(Guid ownerId, QuestionGenerationListQueryDto query);
    Task<JobStatusResponseDto> GetJobAsync(Guid jobId, Guid ownerId);
    Task<object> UpdatePlanAsync(Guid jobId, Guid ownerId, UpdatePlanRequestDto dto);
    Task<JobStatusResponseDto> ApprovePlanAsync(Guid jobId, Guid ownerId);
    Task<JobQuestionsResponseDto> GetQuestionsAsync(Guid jobId, Guid ownerId);
    Task<GeneratedQuestionResponseDto> UpdateQuestionAsync(Guid jobId, Guid questionId, Guid ownerId, UpdateQuestionRequestDto dto);
    Task<GeneratedQuestionResponseDto> AddQuestionAsync(Guid jobId, Guid ownerId, CreateQuestionRequestDto dto);
    Task DeleteQuestionAsync(Guid jobId, Guid questionId, Guid ownerId);
    Task<IReadOnlyList<GeneratedQuestionResponseDto>> ReorderQuestionsAsync(Guid jobId, Guid ownerId, ReorderQuestionsRequestDto dto);
    Task<JobStatusResponseDto> RetryPlanAsync(Guid jobId, Guid ownerId);
    Task<JobStatusResponseDto> RetryQuestionsAsync(Guid jobId, Guid ownerId);
}
