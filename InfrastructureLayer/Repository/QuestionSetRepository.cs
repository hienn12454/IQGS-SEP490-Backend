using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class QuestionSetRepository : IQuestionSetRepository
{
    private readonly Database.AppDbContext _context;

    public QuestionSetRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsBySourceJobIdAsync(Guid sourceJobId)
        => _context.QuestionSets.AnyAsync(qs => qs.SourceJobId == sourceJobId);

    public async Task AddAsync(QuestionSet questionSet, IEnumerable<QuestionSetQuestion> questions)
    {
        await _context.QuestionSets.AddAsync(questionSet);
        await _context.QuestionSetQuestions.AddRangeAsync(questions);
        await _context.SaveChangesAsync();
    }

    public Task<QuestionSet?> GetByIdWithQuestionsAsync(Guid id)
        => _context.QuestionSets
            .Include(qs => qs.Questions.OrderBy(q => q.Order))
            .FirstOrDefaultAsync(qs => qs.Id == id);

    public async Task<HashSet<Guid>> GetSourceJobIdsWithDraftAsync(IEnumerable<Guid> sourceJobIds)
    {
        var ids = sourceJobIds.ToList();
        if (ids.Count == 0)
            return new HashSet<Guid>();

        var matched = await _context.QuestionSets
            .Where(qs => ids.Contains(qs.SourceJobId))
            .Select(qs => qs.SourceJobId)
            .ToListAsync();

        return matched.ToHashSet();
    }
}
