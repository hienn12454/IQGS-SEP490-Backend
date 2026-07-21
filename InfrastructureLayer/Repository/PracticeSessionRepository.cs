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

    public Task<int?> GetTimeLimitMinutesAsync(Guid questionSetId)
        => _context.QuestionSets
            .AsNoTracking()
            .Where(qs => qs.Id == questionSetId)
            .Select(qs => qs.TimeLimitMinutes)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<PracticeSession>> GetInProgressWithTimeLimitAsync()
        => await _context.PracticeSessions
            .Include(s => s.QuestionSet)
            .Where(s => s.Status == PracticeSessionStatus.InProgress
                && s.StartedAt != null
                && s.QuestionSet.TimeLimitMinutes != null)
            .ToListAsync();

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
                CompanyLogo = x.Company.LogoUrl,
                CompanyWebsite = x.Company.WebsiteUrl,
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

        // Làm tròn 2 chữ số thập phân (vd 3.17) — AVG() trên Postgres trả về số thập phân dài (vd 51.0739292829),
        // giới hạn về đúng độ chính xác hiển thị cho candidate, khớp với OverallScore mỗi phiên (PracticeOverallScoreCalculator).
        var scoredQuery = query.Where(s => s.OverallScore != null);
        var averageScore = await scoredQuery.AnyAsync()
            ? Math.Round(await scoredQuery.AverageAsync(s => s.OverallScore!.Value), 2)
            : (double?)null;
        var bestScore = await scoredQuery.AnyAsync()
            ? Math.Round(await scoredQuery.MaxAsync(s => s.OverallScore!.Value), 2)
            : (double?)null;
        var latestScoreRaw = await scoredQuery
            .OrderByDescending(s => s.CompletedAt)
            .Select(s => s.OverallScore)
            .FirstOrDefaultAsync();
        var latestScore = latestScoreRaw.HasValue ? Math.Round(latestScoreRaw.Value, 2) : (double?)null;

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
