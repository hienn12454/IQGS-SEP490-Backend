using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IQuestionSetJdFitReviewRepository
{
    Task<QuestionSetJdFitReview?> GetByQuestionSetIdAsync(Guid questionSetId);
    Task AddAsync(QuestionSetJdFitReview review);
    Task UpdateAsync(QuestionSetJdFitReview review);
}
