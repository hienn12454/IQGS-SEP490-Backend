using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class QuestionSetJdFitReviewRepository : IQuestionSetJdFitReviewRepository
{
    private readonly Database.AppDbContext _context;

    public QuestionSetJdFitReviewRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<QuestionSetJdFitReview?> GetByQuestionSetIdAsync(Guid questionSetId)
        => _context.QuestionSetJdFitReviews.FirstOrDefaultAsync(r => r.QuestionSetId == questionSetId);

    public async Task AddAsync(QuestionSetJdFitReview review)
    {
        await _context.QuestionSetJdFitReviews.AddAsync(review);
        await _context.SaveChangesAsync();
    }

    public Task UpdateAsync(QuestionSetJdFitReview review)
    {
        review.UpdatedAt = DateTime.UtcNow;
        _context.QuestionSetJdFitReviews.Update(review);
        return _context.SaveChangesAsync();
    }
}
