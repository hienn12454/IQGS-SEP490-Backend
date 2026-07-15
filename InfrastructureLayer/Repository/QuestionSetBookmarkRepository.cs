using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class QuestionSetBookmarkRepository : IQuestionSetBookmarkRepository
{
    private readonly Database.AppDbContext _context;

    public QuestionSetBookmarkRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<QuestionSetBookmark?> GetAsync(Guid candidateUserId, Guid questionSetId)
        => _context.QuestionSetBookmarks.FirstOrDefaultAsync(b =>
            b.CandidateUserId == candidateUserId && b.QuestionSetId == questionSetId);

    public async Task AddAsync(QuestionSetBookmark bookmark)
    {
        await _context.QuestionSetBookmarks.AddAsync(bookmark);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(QuestionSetBookmark bookmark)
    {
        _context.QuestionSetBookmarks.Remove(bookmark);
        await _context.SaveChangesAsync();
    }
}
