using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IQuestionGenerationJobRepository
{
    Task<QuestionGenerationJob?> GetByIdAsync(Guid id);
    Task<QuestionGenerationJob?> GetByIdWithPlanAndQuestionsAsync(Guid id);
    Task AddAsync(QuestionGenerationJob job);
    Task UpdateAsync(QuestionGenerationJob job);
    Task AddPlanAsync(QuestionGenerationPlan plan);
    Task UpdatePlanAsync(QuestionGenerationPlan plan);
    Task AddQuestionsAsync(IEnumerable<GeneratedQuestion> questions);
    Task DeleteQuestionsByJobIdAsync(Guid jobId);
}
