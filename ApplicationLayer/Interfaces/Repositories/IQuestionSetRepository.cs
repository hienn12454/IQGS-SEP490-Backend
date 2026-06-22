using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IQuestionSetRepository
{
    Task<bool> ExistsBySourceJobIdAsync(Guid sourceJobId);
    Task AddAsync(QuestionSet questionSet, IEnumerable<QuestionSetQuestion> questions);
    Task<QuestionSet?> GetByIdWithQuestionsAsync(Guid id);
    Task<HashSet<Guid>> GetSourceJobIdsWithDraftAsync(IEnumerable<Guid> sourceJobIds);
}
