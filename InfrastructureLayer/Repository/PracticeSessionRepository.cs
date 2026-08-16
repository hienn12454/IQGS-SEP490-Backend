using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Hr;
using ApplicationLayer.DTOs.QuestionSet;
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
        => _context.PracticeSessions
            .Include(s => s.QuestionSet)
            .FirstOrDefaultAsync(s => s.Id == id);

    public Task<PracticeSession?> GetInProgressByQuestionSetAsync(Guid candidateUserId, Guid questionSetId)
        => _context.PracticeSessions.FirstOrDefaultAsync(s =>
            s.CandidateUserId == candidateUserId &&
            s.QuestionSetId == questionSetId &&
            s.Status == PracticeSessionStatus.InProgress);

    public Task<bool> HasCompletedSessionAsync(Guid candidateUserId, Guid questionSetId)
        => _context.PracticeSessions.AnyAsync(s =>
            s.CandidateUserId == candidateUserId &&
            s.QuestionSetId == questionSetId &&
            s.IsActive &&
            s.Status == PracticeSessionStatus.Completed);

    public Task UpdateAsync(PracticeSession session)
    {
        _context.PracticeSessions.Update(session);
        return _context.SaveChangesAsync();
    }

    public async Task<int> AbandonInProgressByQuestionSetAsync(Guid questionSetId)
    {
        var now = DateTime.UtcNow;
        return await _context.PracticeSessions
            .Where(s => s.QuestionSetId == questionSetId && s.Status == PracticeSessionStatus.InProgress)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, PracticeSessionStatus.Abandoned)
                .SetProperty(x => x.UpdatedAt, now));
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

    public async Task<IReadOnlyList<QuestionSetPractitionerRow>> ListPractitionersByQuestionSetAsync(Guid questionSetId)
    {
        var query = _context.PracticeSessions
            .AsNoTracking()
            .Where(s => s.QuestionSetId == questionSetId && s.IsActive);

        var projected = query
            .Join(_context.Users.AsNoTracking(),
                s => s.CandidateUserId, u => u.Id,
                (s, u) => new { s, u })
            .Join(_context.CandidateProfiles.AsNoTracking(),
                x => x.s.CandidateUserId, p => p.UserId,
                (x, p) => new { x.s, x.u, p })
            // AC-03 SCRUM-326: bám đúng cơ chế consent hiện có — candidate tắt AllowRecruiterRecommendation
            // thì HR không được thấy PII của candidate đó, kể cả trên chính bộ câu hỏi của HR.
            .Where(x => x.p.AllowRecruiterRecommendation)
            .OrderByDescending(x => x.s.StartedAt)
            .Select(x => new QuestionSetPractitionerRow
            {
                SessionId = x.s.Id,
                CandidateUserId = x.s.CandidateUserId,
                CandidateName = x.u.FullName,
                CandidateEmail = x.u.Email,
                TargetRole = x.p.TargetRole,
                SeniorityLevel = x.p.SeniorityLevel,
                Status = x.s.Status,
                OverallScore = x.s.OverallScore,
                StartedAt = x.s.StartedAt,
                CompletedAt = x.s.CompletedAt
            });

        return await projected.ToListAsync();
    }

    public Task<bool> HasAnySessionOnHrOwnedSetsAsync(Guid candidateUserId, Guid hrOwnerId)
        => _context.PracticeSessions
            .AsNoTracking()
            .Where(s => s.CandidateUserId == candidateUserId && s.IsActive)
            .Join(_context.QuestionSets.AsNoTracking(),
                s => s.QuestionSetId, qs => qs.Id,
                (s, qs) => qs)
            .AnyAsync(qs => qs.OwnerId == hrOwnerId);

    public async Task<IReadOnlyList<HrCandidatePracticeOnMySetRow>> ListSessionsOnHrOwnedSetsAsync(
        Guid candidateUserId, Guid hrOwnerId)
    {
        return await _context.PracticeSessions
            .AsNoTracking()
            .Where(s => s.CandidateUserId == candidateUserId && s.IsActive)
            .Join(_context.QuestionSets.AsNoTracking(),
                s => s.QuestionSetId, qs => qs.Id,
                (s, qs) => new { s, qs })
            .Where(x => x.qs.OwnerId == hrOwnerId)
            .OrderByDescending(x => x.s.StartedAt)
            .Select(x => new HrCandidatePracticeOnMySetRow
            {
                SessionId = x.s.Id,
                QuestionSetId = x.qs.Id,
                Title = x.qs.Title ?? string.Empty,
                SessionStatus = x.s.Status,
                OverallScore = x.s.OverallScore,
                StartedAt = x.s.StartedAt,
                CompletedAt = x.s.CompletedAt
            })
            .ToListAsync();
    }

    /// <summary>SCRUM-401: chỉ lấy 2 mốc thời gian COMPLETED trong cửa sổ — aggregate ngày ở service.</summary>
    public async Task<IReadOnlyList<PracticeSessionHeatmapRawRow>> ListCompletedTimestampsForHeatmapAsync(
        Guid candidateUserId, DateTime fromUtc)
    {
        return await _context.PracticeSessions
            .AsNoTracking()
            .Where(s =>
                s.CandidateUserId == candidateUserId
                && s.Status == PracticeSessionStatus.Completed
                && s.CompletedAt != null
                && s.StartedAt != null
                && s.CompletedAt >= fromUtc)
            .Select(s => new PracticeSessionHeatmapRawRow
            {
                StartedAt = s.StartedAt!.Value,
                CompletedAt = s.CompletedAt!.Value
            })
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<HrTalentRow> Items, int TotalCount)> ListTalentForHrAsync(
        Guid hrOwnerId,
        int page,
        int pageSize,
        string? keyword,
        Guid? questionSetId,
        string? status,
        double? minScore)
    {
        var query = _context.PracticeSessions
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Join(_context.QuestionSets.AsNoTracking(),
                s => s.QuestionSetId, qs => qs.Id,
                (s, qs) => new { s, qs })
            .Where(x => x.qs.OwnerId == hrOwnerId && x.qs.IsActive)
            .Join(_context.Users.AsNoTracking(),
                x => x.s.CandidateUserId, u => u.Id,
                (x, u) => new { x.s, x.qs, u })
            .Join(_context.CandidateProfiles.AsNoTracking(),
                x => x.s.CandidateUserId, p => p.UserId,
                (x, p) => new { x.s, x.qs, x.u, p })
            .Where(x => x.p.AllowRecruiterRecommendation);

        if (questionSetId.HasValue)
            query = query.Where(x => x.s.QuestionSetId == questionSetId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.s.Status == status.Trim());

        if (minScore.HasValue)
            query = query.Where(x => x.s.OverallScore != null && x.s.OverallScore >= minScore.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.u.FullName, $"%{term}%")
                || EF.Functions.ILike(x.u.Email, $"%{term}%")
                || EF.Functions.ILike(x.qs.Title ?? "", $"%{term}%"));
        }

        var projected = query
            .OrderByDescending(x => x.s.StartedAt)
            .Select(x => new HrTalentRow
            {
                SessionId = x.s.Id,
                CandidateUserId = x.s.CandidateUserId,
                CandidateName = x.u.FullName,
                CandidateEmail = x.u.Email,
                TargetRole = x.p.TargetRole,
                SeniorityLevel = x.p.SeniorityLevel,
                QuestionSetId = x.qs.Id,
                QuestionSetTitle = x.qs.Title ?? string.Empty,
                SessionStatus = x.s.Status,
                OverallScore = x.s.OverallScore,
                StartedAt = x.s.StartedAt,
                CompletedAt = x.s.CompletedAt,
                RecommendationId = _context.CandidateRecommendations
                    .Where(r => r.CandidateUserId == x.s.CandidateUserId
                        && r.QuestionSetId == x.s.QuestionSetId
                        && r.IsActive)
                    .Select(r => (Guid?)r.Id)
                    .FirstOrDefault(),
                RecommendationStatus = _context.CandidateRecommendations
                    .Where(r => r.CandidateUserId == x.s.CandidateUserId
                        && r.QuestionSetId == x.s.QuestionSetId
                        && r.IsActive)
                    .Select(r => r.Status)
                    .FirstOrDefault(),
                InvitationStatus = _context.CandidateInvitations
                    .Where(i => i.IsActive
                        && _context.CandidateRecommendations.Any(r =>
                            r.Id == i.RecommendationId
                            && r.CandidateUserId == x.s.CandidateUserId
                            && r.QuestionSetId == x.s.QuestionSetId
                            && r.IsActive))
                    .Select(i => i.Status)
                    .FirstOrDefault()
            });

        var totalCount = await projected.CountAsync();
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<PracticeSession?> GetBestCompletedSessionOnSetAsync(Guid candidateUserId, Guid questionSetId)
        => _context.PracticeSessions
            .Where(s =>
                s.CandidateUserId == candidateUserId
                && s.QuestionSetId == questionSetId
                && s.IsActive
                && s.Status == PracticeSessionStatus.Completed)
            .OrderByDescending(s => s.OverallScore)
            .ThenByDescending(s => s.CompletedAt)
            .FirstOrDefaultAsync();

    public Task<bool> HasAnySessionOnSetAsync(Guid candidateUserId, Guid questionSetId)
        => _context.PracticeSessions.AnyAsync(s =>
            s.CandidateUserId == candidateUserId
            && s.QuestionSetId == questionSetId
            && s.IsActive);

    public Task<int> CountCompletedOnHrOwnedSetsSinceAsync(Guid hrOwnerId, DateTime fromUtc)
        => _context.PracticeSessions.CountAsync(s =>
            s.IsActive
            && s.Status == PracticeSessionStatus.Completed
            && s.CompletedAt != null
            && s.CompletedAt >= fromUtc
            && s.QuestionSet.OwnerId == hrOwnerId
            && s.QuestionSet.IsActive);

    public async Task<IReadOnlyList<SetLastScoreDto>> ListLatestCompletedScoresAsync(
        Guid candidateUserId, IReadOnlyList<Guid> questionSetIds)
    {
        if (questionSetIds.Count == 0)
            return Array.Empty<SetLastScoreDto>();

        var rows = await _context.PracticeSessions.AsNoTracking()
            .Where(s =>
                s.CandidateUserId == candidateUserId
                && s.IsActive
                && s.Status == PracticeSessionStatus.Completed
                && questionSetIds.Contains(s.QuestionSetId))
            .Select(s => new { s.QuestionSetId, s.OverallScore, s.CompletedAt })
            .ToListAsync();

        return rows
            .GroupBy(s => s.QuestionSetId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(x => x.CompletedAt).First();
                return new SetLastScoreDto
                {
                    QuestionSetId = g.Key,
                    Score = latest.OverallScore,
                    CompletedAt = latest.CompletedAt
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SetAvgDurationDto>> ListAverageCompletionMinutesAsync(
        IReadOnlyList<Guid> questionSetIds)
    {
        if (questionSetIds.Count == 0)
            return Array.Empty<SetAvgDurationDto>();

        var rows = await _context.PracticeSessions.AsNoTracking()
            .Where(s =>
                s.IsActive
                && s.Status == PracticeSessionStatus.Completed
                && s.StartedAt != null
                && s.CompletedAt != null
                && questionSetIds.Contains(s.QuestionSetId)
                && s.CompletedAt > s.StartedAt)
            .Select(s => new { s.QuestionSetId, s.StartedAt, s.CompletedAt })
            .ToListAsync();

        return rows
            .GroupBy(s => s.QuestionSetId)
            .Select(g => new SetAvgDurationDto
            {
                QuestionSetId = g.Key,
                AvgCompletionMinutes = (int)Math.Round(g.Average(x =>
                    (x.CompletedAt!.Value - x.StartedAt!.Value).TotalMinutes))
            })
            .Where(x => x.AvgCompletionMinutes > 0)
            .ToList();
    }

    public async Task<IReadOnlyList<CandidateSkillStatDto>> ListSkillStatsAsync(Guid candidateUserId)
    {
        var rows = await (
            from fb in _context.AiFeedbacks.AsNoTracking()
            join ans in _context.CandidateAnswers.AsNoTracking() on fb.CandidateAnswerId equals ans.Id
            join sess in _context.PracticeSessions.AsNoTracking() on ans.PracticeSessionId equals sess.Id
            join q in _context.QuestionSetQuestions.AsNoTracking() on ans.QuestionSetQuestionId equals q.Id
            where sess.CandidateUserId == candidateUserId
                  && sess.Status == PracticeSessionStatus.Completed
                  && sess.IsActive
                  && fb.Score != null
                  && q.Skill != null
                  && q.Skill != ""
            select new { q.Skill, Score = fb.Score!.Value }
        ).ToListAsync();

        return rows
            .GroupBy(x => x.Skill!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CandidateSkillStatDto
            {
                Skill = g.Key,
                AverageScore = Math.Round(g.Average(x => x.Score), 1),
                SampleCount = g.Count()
            })
            .OrderBy(x => x.AverageScore)
            .ToList();
    }
}
