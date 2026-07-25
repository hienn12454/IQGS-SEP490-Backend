using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class HrQuestionSetBookmarkRepository : IHrQuestionSetBookmarkRepository
{
    private readonly Database.AppDbContext _context;

    public HrQuestionSetBookmarkRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<HrQuestionSetBookmark?> GetAsync(Guid hrUserId, Guid questionSetId)
        => _context.HrQuestionSetBookmarks.FirstOrDefaultAsync(b =>
            b.HrUserId == hrUserId && b.QuestionSetId == questionSetId);

    public async Task AddAsync(HrQuestionSetBookmark bookmark)
    {
        await _context.HrQuestionSetBookmarks.AddAsync(bookmark);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(HrQuestionSetBookmark bookmark)
    {
        _context.HrQuestionSetBookmarks.Remove(bookmark);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<QuestionSet>> ListBookmarkedQuestionSetsAsync(Guid hrUserId)
    {
        var bookmarkedIds = _context.HrQuestionSetBookmarks
            .AsNoTracking()
            .Where(b => b.HrUserId == hrUserId)
            .Select(b => b.QuestionSetId);

        return await _context.QuestionSets
            .AsNoTracking()
            .Where(qs => bookmarkedIds.Contains(qs.Id))
            .OrderByDescending(qs => qs.CreatedAt)
            .ToListAsync();
    }
}
