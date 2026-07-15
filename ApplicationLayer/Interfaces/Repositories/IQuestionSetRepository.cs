using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

/// <summary>HR quản lý/ghi trên question_sets. Phần đọc marketplace của Candidate ở ICandidateMarketplaceRepository.</summary>
public interface IQuestionSetRepository
{
    Task<bool> ExistsBySourceJobIdAsync(Guid sourceJobId);
    Task<Guid?> GetIdBySourceJobIdAsync(Guid sourceJobId);
    Task<QuestionSet?> GetBySourceJobIdWithQuestionsAsync(Guid sourceJobId);
    Task AddAsync(QuestionSet questionSet, IEnumerable<QuestionSetQuestion> questions);
    Task<QuestionSet?> GetByIdWithQuestionsAsync(Guid id);
    Task UpdateAsync(QuestionSet questionSet);
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
