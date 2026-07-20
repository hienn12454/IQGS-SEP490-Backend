using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class CandidateAnswerRepository : ICandidateAnswerRepository
{
    private readonly Database.AppDbContext _context;

    public CandidateAnswerRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<CandidateAnswer?> GetAsync(Guid practiceSessionId, Guid questionSetQuestionId)
        => _context.CandidateAnswers.FirstOrDefaultAsync(a =>
            a.PracticeSessionId == practiceSessionId && a.QuestionSetQuestionId == questionSetQuestionId);

    public async Task AddAsync(CandidateAnswer answer)
    {
        await _context.CandidateAnswers.AddAsync(answer);
        await _context.SaveChangesAsync();
    }

    public Task UpdateAsync(CandidateAnswer answer)
    {
        _context.CandidateAnswers.Update(answer);
        return _context.SaveChangesAsync();
    }

    public Task<Dictionary<Guid, string>> GetAnswersBySessionIdAsync(Guid practiceSessionId)
        => _context.CandidateAnswers
            .AsNoTracking()
            .Where(a => a.PracticeSessionId == practiceSessionId)
            .ToDictionaryAsync(a => a.QuestionSetQuestionId, a => a.AnswerText);

    public async Task<IReadOnlyList<CandidateAnswer>> GetEntitiesBySessionIdAsync(Guid practiceSessionId)
        => await _context.CandidateAnswers
            .AsNoTracking()
            .Where(a => a.PracticeSessionId == practiceSessionId)
            .ToListAsync();
}
