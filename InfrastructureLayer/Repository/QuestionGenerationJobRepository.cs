using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class QuestionGenerationJobRepository : IQuestionGenerationJobRepository
{
    private readonly Database.AppDbContext _context;

    public QuestionGenerationJobRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<QuestionGenerationJob?> GetByIdAsync(Guid id)
        => _context.QuestionGenerationJobs.FirstOrDefaultAsync(j => j.Id == id);

    public Task<QuestionGenerationJob?> GetByIdWithPlanAndQuestionsAsync(Guid id)
        => _context.QuestionGenerationJobs
            .Include(j => j.Plan)
            .Include(j => j.Questions.OrderBy(q => q.Order))
            .FirstOrDefaultAsync(j => j.Id == id);

    public async Task AddAsync(QuestionGenerationJob job)
    {
        await _context.QuestionGenerationJobs.AddAsync(job);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(QuestionGenerationJob job)
    {
        job.UpdatedAt = DateTime.UtcNow;
        _context.QuestionGenerationJobs.Update(job);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteJobAsync(QuestionGenerationJob job)
    {
        _context.QuestionGenerationJobs.Remove(job);
        await _context.SaveChangesAsync();
    }

    public async Task AddPlanAsync(QuestionGenerationPlan plan)
    {
        await _context.QuestionGenerationPlans.AddAsync(plan);
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePlanAsync(QuestionGenerationPlan plan)
    {
        plan.UpdatedAt = DateTime.UtcNow;
        _context.QuestionGenerationPlans.Update(plan);
        await _context.SaveChangesAsync();
    }

    public async Task AddQuestionsAsync(IEnumerable<GeneratedQuestion> questions)
    {
        await _context.GeneratedQuestions.AddRangeAsync(questions);
        await _context.SaveChangesAsync();
    }


    public async Task<PagedResultDto<QuestionGenerationJob>> GetPagedByOwnerAsync(
        Guid ownerId, QuestionGenerationListQueryDto query)
    {
        var q = _context.QuestionGenerationJobs
            .AsNoTracking()
            .Include(j => j.Questions)
            .Where(j => j.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(j => j.Status == query.Status.Trim());

        if (query.FromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(query.FromDate.Value.Date, DateTimeKind.Utc);
            q = q.Where(j => j.CreatedAt >= from);
        }

        if (query.ToDate.HasValue)
        {
            var toExclusive = DateTime.SpecifyKind(query.ToDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            q = q.Where(j => j.CreatedAt < toExclusive);
        }

        var total = await q.CountAsync();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await q
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<QuestionGenerationJob>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResultDto<QuestionGenerationJob>> GetPagedPlansByOwnerAsync(
        Guid ownerId, QuestionGenerationListQueryDto query)
    {
        var q = _context.QuestionGenerationJobs
            .AsNoTracking()
            .Include(j => j.Plan)
            .Where(j => j.OwnerId == ownerId && j.Plan != null);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(j => j.Status == query.Status.Trim());

        if (query.FromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(query.FromDate.Value.Date, DateTimeKind.Utc);
            q = q.Where(j => j.Plan!.CreatedAt >= from);
        }

        if (query.ToDate.HasValue)
        {
            var toExclusive = DateTime.SpecifyKind(query.ToDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            q = q.Where(j => j.Plan!.CreatedAt < toExclusive);
        }

        var total = await q.CountAsync();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await q
            .OrderByDescending(j => j.Plan!.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<QuestionGenerationJob>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<GeneratedQuestion?> GetQuestionByIdAsync(Guid questionId)
        => _context.GeneratedQuestions.FirstOrDefaultAsync(q => q.Id == questionId);

    public Task<List<GeneratedQuestion>> GetQuestionsByJobIdAsync(Guid jobId)
        => _context.GeneratedQuestions.Where(q => q.JobId == jobId).OrderBy(q => q.Order).ToListAsync();

    public async Task UpdateQuestionAsync(GeneratedQuestion question)
    {
        question.UpdatedAt = DateTime.UtcNow;
        _context.GeneratedQuestions.Update(question);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteQuestionAsync(GeneratedQuestion question)
    {
        _context.GeneratedQuestions.Remove(question);
        await _context.SaveChangesAsync();
    }

    public Task<int> GetQuestionCountByJobIdAsync(Guid jobId)
        => _context.GeneratedQuestions.CountAsync(q => q.JobId == jobId);

    public async Task<int> GetMaxOrderByJobIdAsync(Guid jobId)
    {
        var max = await _context.GeneratedQuestions
            .Where(q => q.JobId == jobId)
            .Select(q => (int?)q.Order)
            .MaxAsync();
        return max ?? 0;
    }

    public async Task DeleteQuestionsByJobIdAsync(Guid jobId)
    {
        var questions = await _context.GeneratedQuestions.Where(q => q.JobId == jobId).ToListAsync();
        _context.GeneratedQuestions.RemoveRange(questions);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<QuestionGenerationJob>> GetStuckByStatusesAsync(
        IReadOnlyList<string> statuses, DateTime updatedBeforeUtc)
        => await _context.QuestionGenerationJobs
            .Where(j => statuses.Contains(j.Status)
                && j.UpdatedAt != null
                && j.UpdatedAt < updatedBeforeUtc)
            .ToListAsync();

    public Task<QuestionGenerationJob?> GetOwnedJobWithQuestionAsync(
        Guid jobId, Guid questionId, Guid ownerId)
        => _context.QuestionGenerationJobs
            .Include(j => j.Plan)
            .Include(j => j.Questions)
            .FirstOrDefaultAsync(j =>
                j.Id == jobId
                && j.OwnerId == ownerId
                && j.Questions.Any(q => q.Id == questionId));

    public async Task<List<QuestionAiChatMessage>> GetQuestionAiChatMessagesAsync(
        Guid jobId, Guid questionId, int limit)
    {
        var all = await _context.QuestionAiChatMessages
            .AsNoTracking()
            .Where(m => m.JobId == jobId && m.QuestionId == questionId && m.IsActive)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        if (all.Count <= limit)
            return all;

        return all.Skip(all.Count - limit).ToList();
    }

    public async Task AddQuestionAiChatMessagesAsync(IEnumerable<QuestionAiChatMessage> messages)
    {
        await _context.QuestionAiChatMessages.AddRangeAsync(messages);
        await _context.SaveChangesAsync();
    }
}
