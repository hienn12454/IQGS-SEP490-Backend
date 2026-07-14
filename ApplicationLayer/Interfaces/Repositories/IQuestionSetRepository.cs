using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IQuestionSetRepository
{
    Task<bool> ExistsBySourceJobIdAsync(Guid sourceJobId);
    Task<Guid?> GetIdBySourceJobIdAsync(Guid sourceJobId);
    Task<QuestionSet?> GetBySourceJobIdWithQuestionsAsync(Guid sourceJobId);
    Task AddAsync(QuestionSet questionSet, IEnumerable<QuestionSetQuestion> questions);
    Task<QuestionSet?> GetByIdWithQuestionsAsync(Guid id);
    Task<HashSet<Guid>> GetSourceJobIdsWithDraftAsync(IEnumerable<Guid> sourceJobIds);
    Task<IReadOnlyList<QuestionSet>> ListByOwnerAsync(Guid ownerId, Guid? sourceJobId = null);
    Task<QuestionSetQuestion?> GetQuestionByIdAsync(Guid questionId);
    Task<List<QuestionSetQuestion>> GetQuestionsByQuestionSetIdAsync(Guid questionSetId);
    Task<int> GetQuestionCountByQuestionSetIdAsync(Guid questionSetId);
    Task<int> GetMaxOrderByQuestionSetIdAsync(Guid questionSetId);
    Task AddQuestionAsync(QuestionSetQuestion question);
    Task UpdateQuestionAsync(QuestionSetQuestion question);
    Task DeleteQuestionAsync(QuestionSetQuestion question);
}
