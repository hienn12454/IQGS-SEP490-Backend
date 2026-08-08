using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace InfrastructureLayer.Repository;

/// <summary>V1 generate tables dropped — all methods no-op / empty (API returns 410).</summary>
public class QuestionGenerationJobRepository : IQuestionGenerationJobRepository
{
    private static InvalidOperationException Retired()
        => new("Generate V1 đã retire. Dùng Studio /api/studio/projects.");

    public Task<QuestionGenerationJob?> GetByIdAsync(Guid id)
        => Task.FromResult<QuestionGenerationJob?>(null);

    public Task<QuestionGenerationJob?> GetByIdWithPlanAndQuestionsAsync(Guid id)
        => Task.FromResult<QuestionGenerationJob?>(null);

    public Task AddAsync(QuestionGenerationJob job) => throw Retired();
    public Task UpdateAsync(QuestionGenerationJob job) => throw Retired();
    public Task DeleteJobAsync(QuestionGenerationJob job) => throw Retired();
    public Task AddPlanAsync(QuestionGenerationPlan plan) => throw Retired();
    public Task UpdatePlanAsync(QuestionGenerationPlan plan) => throw Retired();
    public Task AddQuestionsAsync(IEnumerable<GeneratedQuestion> questions) => throw Retired();
    public Task DeleteQuestionsByJobIdAsync(Guid jobId) => Task.CompletedTask;

    public Task<PagedResultDto<QuestionGenerationJob>> GetPagedByOwnerAsync(Guid ownerId, QuestionGenerationListQueryDto query)
        => Task.FromResult(new PagedResultDto<QuestionGenerationJob>
        {
            Items = Array.Empty<QuestionGenerationJob>(),
            TotalCount = 0,
            Page = query.Page,
            PageSize = query.PageSize
        });

    public Task<IReadOnlyList<QuestionGenerationJob>> GetAllByOwnerWithPlanAndQuestionsAsync(Guid ownerId)
        => Task.FromResult<IReadOnlyList<QuestionGenerationJob>>(Array.Empty<QuestionGenerationJob>());

    public Task<PagedResultDto<QuestionGenerationJob>> GetPagedPlansByOwnerAsync(Guid ownerId, QuestionGenerationListQueryDto query)
        => GetPagedByOwnerAsync(ownerId, query);

    public Task<GeneratedQuestion?> GetQuestionByIdAsync(Guid questionId)
        => Task.FromResult<GeneratedQuestion?>(null);

    public Task<List<GeneratedQuestion>> GetQuestionsByJobIdAsync(Guid jobId)
        => Task.FromResult(new List<GeneratedQuestion>());

    public Task UpdateQuestionAsync(GeneratedQuestion question) => throw Retired();
    public Task DeleteQuestionAsync(GeneratedQuestion question) => throw Retired();
    public Task<int> GetQuestionCountByJobIdAsync(Guid jobId) => Task.FromResult(0);
    public Task<int> GetMaxOrderByJobIdAsync(Guid jobId) => Task.FromResult(0);

    public Task<IReadOnlyList<QuestionGenerationJob>> GetStuckByStatusesAsync(
        IReadOnlyList<string> statuses, DateTime updatedBeforeUtc)
        => Task.FromResult<IReadOnlyList<QuestionGenerationJob>>(Array.Empty<QuestionGenerationJob>());

    public Task<QuestionGenerationJob?> GetOwnedJobWithQuestionAsync(Guid jobId, Guid questionId, Guid ownerId)
        => Task.FromResult<QuestionGenerationJob?>(null);

    public Task<List<QuestionAiChatMessage>> GetQuestionAiChatMessagesAsync(Guid jobId, Guid questionId, int limit)
        => Task.FromResult(new List<QuestionAiChatMessage>());

    public Task AddQuestionAiChatMessagesAsync(IEnumerable<QuestionAiChatMessage> messages) => throw Retired();
}
