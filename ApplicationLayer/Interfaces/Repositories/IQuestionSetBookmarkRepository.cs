using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IQuestionSetBookmarkRepository
{
    Task<QuestionSetBookmark?> GetAsync(Guid candidateUserId, Guid questionSetId);
    Task AddAsync(QuestionSetBookmark bookmark);
    Task DeleteAsync(QuestionSetBookmark bookmark);
}
