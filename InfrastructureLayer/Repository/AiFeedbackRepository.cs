using ApplicationLayer.DTOs.Recommendation;
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

    public async Task<double?> GetPreviousBestSucceededScoreAsync(
        Guid candidateUserId, Guid questionSetQuestionId, Guid excludePracticeSessionId)
    {
        return await _context.AiFeedbacks
            .AsNoTracking()
            .Where(f =>
                f.EvaluationStatus == AiFeedbackEvaluationStatus.Succeeded
                && f.Score != null
                && f.CandidateAnswer.QuestionSetQuestionId == questionSetQuestionId
                && f.CandidateAnswer.PracticeSessionId != excludePracticeSessionId
                && f.CandidateAnswer.PracticeSession.CandidateUserId == candidateUserId
                && f.CandidateAnswer.PracticeSession.Status == PracticeSessionStatus.Completed)
            .OrderByDescending(f => f.CandidateAnswer.PracticeSession.CompletedAt)
            .Select(f => (double?)f.Score!.Value)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<SkillScoreDto>> GetSkillAveragesBySessionAsync(Guid practiceSessionId)
    {
        return await _context.AiFeedbacks
            .AsNoTracking()
            .Where(f =>
                f.CandidateAnswer.PracticeSessionId == practiceSessionId
                && f.EvaluationStatus == AiFeedbackEvaluationStatus.Succeeded
                && f.Score != null
                && f.CandidateAnswer.QuestionSetQuestion.Skill != null
                && f.CandidateAnswer.QuestionSetQuestion.Skill != "")
            .GroupBy(f => f.CandidateAnswer.QuestionSetQuestion.Skill!)
            .Select(g => new SkillScoreDto
            {
                Skill = g.Key,
                AvgScore = Math.Round(g.Average(x => x.Score!.Value), 1),
                QuestionCount = g.Count()
            })
            .OrderBy(x => x.Skill)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> ListWeakQuestionTextsAsync(
        Guid candidateUserId, string skill, double maxScoreExclusive, int take)
    {
        if (take <= 0) return Array.Empty<string>();
        var key = (skill ?? "").Trim().ToLowerInvariant();
        var rows = await _context.AiFeedbacks
            .AsNoTracking()
            .Where(f =>
                f.EvaluationStatus == AiFeedbackEvaluationStatus.Succeeded
                && f.Score != null
                && f.Score < maxScoreExclusive
                && f.CandidateAnswer.PracticeSession.CandidateUserId == candidateUserId
                && f.CandidateAnswer.PracticeSession.Status == PracticeSessionStatus.Completed
                && f.CandidateAnswer.QuestionSetQuestion.Skill != null
                && f.CandidateAnswer.QuestionSetQuestion.Skill != "")
            .OrderByDescending(f => f.CandidateAnswer.PracticeSession.CompletedAt)
            .Select(f => new
            {
                Skill = f.CandidateAnswer.QuestionSetQuestion.Skill!,
                Text = f.CandidateAnswer.QuestionSetQuestion.Question
            })
            .Take(Math.Max(take * 4, 20))
            .ToListAsync();

        return rows
            .Where(r => r.Skill.Trim().ToLowerInvariant() == key || r.Skill.Trim().ToLowerInvariant().Contains(key) || key.Contains(r.Skill.Trim().ToLowerInvariant()))
            .Select(r =>
            {
                var t = (r.Text ?? "").Trim();
                if (t.Length > 240) t = t[..240];
                return t;
            })
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }
}
