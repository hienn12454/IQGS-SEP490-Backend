using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class PracticeSessionRepository : IPracticeSessionRepository
{
    private readonly Database.AppDbContext _context;

    public PracticeSessionRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PracticeSession session)
    {
        await _context.PracticeSessions.AddAsync(session);
        await _context.SaveChangesAsync();
    }

    public Task<PracticeSession?> GetByIdAsync(Guid id)
        => _context.PracticeSessions.FirstOrDefaultAsync(s => s.Id == id);

    public Task<PracticeSession?> GetInProgressByQuestionSetAsync(Guid candidateUserId, Guid questionSetId)
        => _context.PracticeSessions.FirstOrDefaultAsync(s =>
            s.CandidateUserId == candidateUserId &&
            s.QuestionSetId == questionSetId &&
            s.Status == PracticeSessionStatus.InProgress);

    public Task UpdateAsync(PracticeSession session)
    {
        _context.PracticeSessions.Update(session);
        return _context.SaveChangesAsync();
    }

    public async Task<(IReadOnlyList<PracticeSessionRow> Items, int TotalCount)> ListAsync(
        Guid candidateUserId, string? status, Guid? questionSetId, string? keyword,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize)
    {
        var query = _context.PracticeSessions
            .AsNoTracking()
            .Where(s => s.CandidateUserId == candidateUserId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(s => s.Status == status);

        if (questionSetId.HasValue)
            query = query.Where(s => s.QuestionSetId == questionSetId.Value);

        if (fromDate.HasValue)
            query = query.Where(s => s.StartedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(s => s.StartedAt <= toDate.Value);

        var joined = query
            .Join(_context.QuestionSets.AsNoTracking(),
                s => s.QuestionSetId, qs => qs.Id,
                (s, qs) => new { s, qs })
            .Join(_context.HRProfiles.AsNoTracking(),
                x => x.qs.OwnerId, hr => hr.UserId,
                (x, hr) => new { x.s, x.qs, hr })
            .Join(_context.Companies.AsNoTracking(),
                x => x.hr.CompanyId, c => c.Id,
                (x, company) => new { x.s, x.qs, Company = company });

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = $"%{keyword.Trim()}%";
            joined = joined.Where(x =>
                EF.Functions.ILike(x.qs.Title ?? "", term) ||
                EF.Functions.ILike(x.Company.Name, term));
        }

        var projected = joined
            .OrderByDescending(x => x.s.StartedAt)
            .Select(x => new PracticeSessionRow
            {
                SessionId = x.s.Id,
                QuestionSetId = x.qs.Id,
                SetTitle = x.qs.Title,
                CompanyName = x.Company.Name,
                Status = x.s.Status,
                Score = x.s.OverallScore,
                StartedAt = x.s.StartedAt,
                CompletedAt = x.s.CompletedAt
            });

        var totalCount = await projected.CountAsync();
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<PracticeSessionStatsDto> GetStatsAsync(Guid candidateUserId, DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.PracticeSessions
            .AsNoTracking()
            .Where(s => s.CandidateUserId == candidateUserId && s.Status == PracticeSessionStatus.Completed);

        if (fromDate.HasValue)
            query = query.Where(s => s.StartedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(s => s.StartedAt <= toDate.Value);

        var totalSessions = await query.CountAsync();
        if (totalSessions == 0)
            return new PracticeSessionStatsDto { TotalSessions = 0 };

        var scoredQuery = query.Where(s => s.OverallScore != null);
        var averageScore = await scoredQuery.AnyAsync()
            ? await scoredQuery.AverageAsync(s => s.OverallScore)
            : (double?)null;
        var bestScore = await scoredQuery.AnyAsync()
            ? await scoredQuery.MaxAsync(s => s.OverallScore)
            : (double?)null;
        var latestScore = await scoredQuery
            .OrderByDescending(s => s.CompletedAt)
            .Select(s => s.OverallScore)
            .FirstOrDefaultAsync();

        // Postgres/Npgsql không hỗ trợ EF.Functions.DateDiff* — lấy 2 mốc thời gian rồi trừ ở client.
        var timestamps = await query
            .Where(s => s.StartedAt != null && s.CompletedAt != null)
            .Select(s => new { s.StartedAt, s.CompletedAt })
            .ToListAsync();

        var totalDurationSeconds = timestamps.Sum(t => (t.CompletedAt!.Value - t.StartedAt!.Value).TotalSeconds);

        return new PracticeSessionStatsDto
        {
            TotalSessions = totalSessions,
            AverageScore = averageScore,
            BestScore = bestScore,
            LatestScore = latestScore,
            TotalDurationSeconds = (long)totalDurationSeconds
        };
    }
}
