using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class AiFeedbackRepository : IAiFeedbackRepository
{
    private readonly Database.AppDbContext _context;

    public AiFeedbackRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<AiFeedback?> GetByCandidateAnswerIdAsync(Guid candidateAnswerId)
        => _context.AiFeedbacks.FirstOrDefaultAsync(f => f.CandidateAnswerId == candidateAnswerId);

    public async Task AddAsync(AiFeedback feedback)
    {
        await _context.AiFeedbacks.AddAsync(feedback);
        await _context.SaveChangesAsync();
    }

    public Task UpdateAsync(AiFeedback feedback)
    {
        feedback.UpdatedAt = DateTime.UtcNow;
        _context.AiFeedbacks.Update(feedback);
        return _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AiFeedback>> GetBySessionIdAsync(Guid practiceSessionId)
    {
        return await _context.AiFeedbacks
            .AsNoTracking()
            .Where(f => f.CandidateAnswer.PracticeSessionId == practiceSessionId)
            .Include(f => f.CandidateAnswer)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<double>> GetSucceededScoresAsync(Guid practiceSessionId)
    {
        return await _context.AiFeedbacks
            .AsNoTracking()
            .Where(f =>
                f.CandidateAnswer.PracticeSessionId == practiceSessionId
                && f.EvaluationStatus == AiFeedbackEvaluationStatus.Succeeded
                && f.Score != null)
            .Select(f => f.Score!.Value)
            .ToListAsync();
    }
}
