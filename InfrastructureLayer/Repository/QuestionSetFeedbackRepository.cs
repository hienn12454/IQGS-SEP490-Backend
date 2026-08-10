using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class QuestionSetFeedbackRepository : IQuestionSetFeedbackRepository
{
    private readonly Database.AppDbContext _context;

    public QuestionSetFeedbackRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<QuestionSetFeedback?> GetAsync(Guid candidateUserId, Guid questionSetId)
        => _context.QuestionSetFeedbacks.FirstOrDefaultAsync(f =>
            f.CandidateUserId == candidateUserId && f.QuestionSetId == questionSetId && f.IsActive);

    public async Task AddAsync(QuestionSetFeedback feedback)
    {
        await _context.QuestionSetFeedbacks.AddAsync(feedback);
        await _context.SaveChangesAsync();
    }

    public Task UpdateAsync(QuestionSetFeedback feedback)
    {
        feedback.UpdatedAt = DateTime.UtcNow;
        _context.QuestionSetFeedbacks.Update(feedback);
        return _context.SaveChangesAsync();
    }

    public async Task<(List<QuestionSetFeedback> Items, int TotalCount)> GetPagedByQuestionSetAsync(
        Guid questionSetId, int page, int pageSize)
    {
        var query = _context.QuestionSetFeedbacks
            .AsNoTracking()
            .Include(f => f.CandidateUser)
            .Where(f => f.QuestionSetId == questionSetId && f.IsActive);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(double? Average, int Count)> GetSummaryAsync(Guid questionSetId)
    {
        var ratings = await _context.QuestionSetFeedbacks
            .AsNoTracking()
            .Where(f => f.QuestionSetId == questionSetId && f.IsActive)
            .Select(f => f.Rating)
            .ToListAsync();

        if (ratings.Count == 0) return (null, 0);
        return (ratings.Average(), ratings.Count);
    }
}
