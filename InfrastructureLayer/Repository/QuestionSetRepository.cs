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

    public async Task<Guid?> GetIdBySourceJobIdAsync(Guid sourceJobId)
    {
        var id = await _context.QuestionSets
            .Where(qs => qs.SourceJobId == sourceJobId)
            .Select(qs => (Guid?)qs.Id)
            .FirstOrDefaultAsync();
        return id;
    }

    // Lấy question set (draft) theo job nguồn — dùng cho Ask AI sau save-draft
    public Task<QuestionSet?> GetBySourceJobIdWithQuestionsAsync(Guid sourceJobId)
        => _context.QuestionSets
            .Include(qs => qs.Questions.OrderBy(q => q.Order))
            .FirstOrDefaultAsync(qs => qs.SourceJobId == sourceJobId);

    public Task<QuestionSet?> GetBySourceProjectIdWithQuestionsAsync(Guid sourceProjectId)
        => _context.QuestionSets
            .Include(qs => qs.Questions.OrderBy(q => q.Order))
            .FirstOrDefaultAsync(qs => qs.SourceProjectId == sourceProjectId && qs.IsActive);

    public async Task AddAsync(QuestionSet questionSet, IEnumerable<QuestionSetQuestion> questions)
    {
        await _context.QuestionSets.AddAsync(questionSet);
        await _context.QuestionSetQuestions.AddRangeAsync(questions);
        await _context.SaveChangesAsync();
    }

    public async Task ReplaceQuestionsAsync(QuestionSet questionSet, IEnumerable<QuestionSetQuestion> newQuestions)
    {
        var old = await _context.QuestionSetQuestions
            .Where(q => q.QuestionSetId == questionSet.Id)
            .ToListAsync();
        if (old.Count > 0)
            _context.QuestionSetQuestions.RemoveRange(old);

        await _context.QuestionSetQuestions.AddRangeAsync(newQuestions);
        _context.QuestionSets.Update(questionSet);
        await _context.SaveChangesAsync();
    }

    public Task<bool> HasPracticeAnswersAsync(Guid questionSetId)
        => _context.CandidateAnswers.AnyAsync(a =>
            a.QuestionSetQuestion.QuestionSetId == questionSetId);

    public Task<QuestionSet?> GetByIdWithQuestionsAsync(Guid id)
        => _context.QuestionSets
            .Include(qs => qs.Questions.OrderBy(q => q.Order))
            .FirstOrDefaultAsync(qs => qs.Id == id);

    public Task UpdateAsync(QuestionSet questionSet)
    {
        _context.QuestionSets.Update(questionSet);
        return _context.SaveChangesAsync();
    }

    public async Task<HashSet<Guid>> GetSourceJobIdsWithDraftAsync(IEnumerable<Guid> sourceJobIds)
    {
        var ids = sourceJobIds.ToList();
        if (ids.Count == 0)
            return new HashSet<Guid>();

        var matched = await _context.QuestionSets
            .Where(qs => qs.SourceJobId != null && ids.Contains(qs.SourceJobId.Value))
            .Select(qs => qs.SourceJobId!.Value)
            .ToListAsync();

        return matched.ToHashSet();
    }

    public async Task<IReadOnlyList<QuestionSet>> ListByOwnerAsync(Guid ownerId, Guid? sourceJobId = null)
    {
        var query = _context.QuestionSets
            .AsNoTracking()
            .Where(qs => qs.OwnerId == ownerId && qs.IsActive);

        if (sourceJobId.HasValue)
            query = query.Where(qs => qs.SourceJobId == sourceJobId.Value);

        return await query
            .OrderByDescending(qs => qs.CreatedAt)
            .ToListAsync();
    }

    public async Task<Dictionary<Guid, int>> GetQuestionCountsBySetIdsAsync(IEnumerable<Guid> questionSetIds)
    {
        var ids = questionSetIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, int>();

        return await _context.QuestionSetQuestions
            .AsNoTracking()
            .Where(q => ids.Contains(q.QuestionSetId) && q.IsActive)
            .GroupBy(q => q.QuestionSetId)
            .Select(g => new { SetId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SetId, x => x.Count);
    }

    public async Task SoftDeleteAsync(QuestionSet questionSet)
    {
        questionSet.IsActive = false;
        questionSet.UpdatedAt = DateTime.UtcNow;
        _context.QuestionSets.Update(questionSet);
        await _context.SaveChangesAsync();
    }

    public Task<QuestionSetQuestion?> GetQuestionByIdAsync(Guid questionId)
        => _context.QuestionSetQuestions.FirstOrDefaultAsync(q => q.Id == questionId);

    public Task<List<QuestionSetQuestion>> GetQuestionsByQuestionSetIdAsync(Guid questionSetId)
        => _context.QuestionSetQuestions
            .Where(q => q.QuestionSetId == questionSetId)
            .OrderBy(q => q.Order)
            .ToListAsync();

    public Task<int> GetQuestionCountByQuestionSetIdAsync(Guid questionSetId)
        => _context.QuestionSetQuestions.CountAsync(q => q.QuestionSetId == questionSetId);

    public async Task<int> GetMaxOrderByQuestionSetIdAsync(Guid questionSetId)
    {
        var max = await _context.QuestionSetQuestions
            .Where(q => q.QuestionSetId == questionSetId)
            .Select(q => (int?)q.Order)
            .MaxAsync();
        return max ?? 0;
    }

    public async Task AddQuestionAsync(QuestionSetQuestion question)
    {
        await _context.QuestionSetQuestions.AddAsync(question);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateQuestionAsync(QuestionSetQuestion question)
    {
        question.UpdatedAt = DateTime.UtcNow;
        _context.QuestionSetQuestions.Update(question);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteQuestionAsync(QuestionSetQuestion question)
    {
        _context.QuestionSetQuestions.Remove(question);
        await _context.SaveChangesAsync();
    }
}
