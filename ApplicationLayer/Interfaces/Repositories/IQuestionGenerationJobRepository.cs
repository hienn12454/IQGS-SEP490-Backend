using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.DTOs.QuestionGeneration;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IQuestionGenerationJobRepository
{
    Task<QuestionGenerationJob?> GetByIdAsync(Guid id);
    Task<QuestionGenerationJob?> GetByIdWithPlanAndQuestionsAsync(Guid id);
    Task AddAsync(QuestionGenerationJob job);
    Task UpdateAsync(QuestionGenerationJob job);
    Task DeleteJobAsync(QuestionGenerationJob job);
    Task AddPlanAsync(QuestionGenerationPlan plan);
    Task UpdatePlanAsync(QuestionGenerationPlan plan);
    Task AddQuestionsAsync(IEnumerable<GeneratedQuestion> questions);
    Task DeleteQuestionsByJobIdAsync(Guid jobId);
    Task<PagedResultDto<QuestionGenerationJob>> GetPagedByOwnerAsync(Guid ownerId, QuestionGenerationListQueryDto query);
    Task<PagedResultDto<QuestionGenerationJob>> GetPagedPlansByOwnerAsync(Guid ownerId, QuestionGenerationListQueryDto query);
    Task<GeneratedQuestion?> GetQuestionByIdAsync(Guid questionId);
    Task<List<GeneratedQuestion>> GetQuestionsByJobIdAsync(Guid jobId);
    Task UpdateQuestionAsync(GeneratedQuestion question);
    Task DeleteQuestionAsync(GeneratedQuestion question);
    Task<int> GetQuestionCountByJobIdAsync(Guid jobId);
    Task<int> GetMaxOrderByJobIdAsync(Guid jobId);
    Task<IReadOnlyList<QuestionGenerationJob>> GetStuckByStatusesAsync(
        IReadOnlyList<string> statuses, DateTime updatedBeforeUtc);
}
