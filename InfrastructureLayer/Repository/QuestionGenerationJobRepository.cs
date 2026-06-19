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

    public async Task DeleteQuestionsByJobIdAsync(Guid jobId)
    {
        var questions = await _context.GeneratedQuestions.Where(q => q.JobId == jobId).ToListAsync();
        _context.GeneratedQuestions.RemoveRange(questions);
        await _context.SaveChangesAsync();
    }
}
