using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

/// <summary>SCRUM-404 / SCRUM-480: query Marketplace cho Admin (owner HR + practice aggregates).</summary>
public class AdminMarketplaceRepository : IAdminMarketplaceRepository
{
    private readonly Database.AppDbContext _context;

    public AdminMarketplaceRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// SCRUM-480: LEFT JOIN Users / HRProfiles / Companies — set orphan vẫn hiện trong list
    /// để TotalPublished (stats) khớp totalCount danh sách.
    /// Chỉ Kind=Marketplace — loại Personal (AI Coaching diagnostic/drill/reassessment),
    /// gồm cả bộ cũ đã Published với Kind=Personal.
    /// </summary>
    private IQueryable<AdminMarketplaceJoin> PublishedJoinQuery()
        => from qs in _context.QuestionSets.AsNoTracking()
           where qs.Status == QuestionSetStatus.Published
                 && qs.IsActive
                 && qs.Kind == QuestionSetKind.Marketplace
           join u in _context.Users.AsNoTracking() on qs.OwnerId equals u.Id into ug
           from u in ug.DefaultIfEmpty()
           join hr in _context.HRProfiles.AsNoTracking() on qs.OwnerId equals hr.UserId into hrg
           from hr in hrg.DefaultIfEmpty()
           join c in _context.Companies.AsNoTracking() on hr.CompanyId equals c.Id into cg
           from c in cg.DefaultIfEmpty()
           select new AdminMarketplaceJoin
           {
               QuestionSet = qs,
               HrUser = u,
               Company = c
           };

    /// <summary>Bộ HR publish trên Marketplace — không gồm Personal/Coach.</summary>
    private IQueryable<QuestionSet> MarketplacePublishedQuery()
        => _context.QuestionSets.AsNoTracking()
            .Where(qs => qs.IsActive
                         && qs.Status == QuestionSetStatus.Published
                         && qs.Kind == QuestionSetKind.Marketplace);

    private sealed class AdminMarketplaceJoin
    {
        public QuestionSet QuestionSet { get; set; } = null!;
        public User? HrUser { get; set; }
        public Company? Company { get; set; }
    }

    private IQueryable<AdminMarketplaceSetRow> ProjectRows(IQueryable<AdminMarketplaceJoin> query)
        => query.Select(x => new AdminMarketplaceSetRow
        {
            Id = x.QuestionSet.Id,
            Title = x.QuestionSet.Title,
            Description = x.QuestionSet.HrNote != null
                && (EF.Functions.ILike(x.QuestionSet.HrNote, MarketplaceDescriptionHelper.StudioSavePrefix + "%")
                    || EF.Functions.ILike(x.QuestionSet.HrNote, MarketplaceDescriptionHelper.StudioMirrorPrefix + "%"))
                ? null
                : x.QuestionSet.HrNote,
            HrUserId = x.QuestionSet.OwnerId,
            HrName = x.HrUser != null ? x.HrUser.FullName : null,
            HrEmail = x.HrUser != null ? x.HrUser.Email : null,
            CompanyId = x.Company != null ? x.Company.Id : null,
            CompanyName = x.Company != null ? x.Company.Name : null,
            CompanyLogo = x.Company != null ? x.Company.LogoUrl : null,
            CompanyWebsite = x.Company != null ? x.Company.WebsiteUrl : null,
            Difficulty = x.QuestionSet.Questions
                .Where(q => q.IsActive && q.Difficulty != null && q.Difficulty != "")
                .Select(q => q.Difficulty)
                .GroupBy(d => d)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "medium",
            Skills = x.QuestionSet.Questions
                .Where(q => q.IsActive && q.Skill != null && q.Skill != "")
                .Select(q => q.Skill!)
                .Distinct()
                .OrderBy(s => s)
                .ToList(),
            TotalQuestions = x.QuestionSet.Questions.Count(q => q.IsActive),
            AttemptCount = _context.PracticeSessions
                .Count(ps => ps.QuestionSetId == x.QuestionSet.Id && ps.IsActive),
            UniqueCandidateCount = _context.PracticeSessions
                .Where(ps => ps.QuestionSetId == x.QuestionSet.Id && ps.IsActive)
                .Select(ps => ps.CandidateUserId)
                .Distinct()
                .Count(),
            Rating = _context.PracticeSessions
                .Where(ps => ps.QuestionSetId == x.QuestionSet.Id && ps.IsActive)
                .Average(ps => (double?)ps.OverallScore),
            IsPinned = x.QuestionSet.IsPinned,
            // SCRUM-472: tag Practice / Tuyển cho Admin
            IsHiringAssessment = x.QuestionSet.IsHiringAssessment,
            PinnedAt = x.QuestionSet.PinnedAt,
            PublishedAt = x.QuestionSet.PublishedAt,
            TimeLimitMinutes = x.QuestionSet.TimeLimitMinutes
        });

    private static IQueryable<AdminMarketplaceSetRow> ApplySort(
        IQueryable<AdminMarketplaceSetRow> projected, string sortBy)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "newest" => projected.OrderByDescending(x => x.PublishedAt),
            "most_practiced" => projected
                .OrderByDescending(x => x.AttemptCount)
                .ThenByDescending(x => x.PublishedAt),
            "highest_rated" => projected
                .OrderByDescending(x => x.Rating ?? -1)
                .ThenByDescending(x => x.AttemptCount)
                .ThenByDescending(x => x.PublishedAt),
            _ => projected
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.PinnedAt)
                .ThenByDescending(x => x.PublishedAt)
        };
    }

    public async Task<(IReadOnlyList<AdminMarketplaceSetRow> Items, int TotalCount)> ListPublishedAsync(
        int page, int pageSize, string? keyword, Guid? companyId, Guid? hrUserId,
        bool? isHiringAssessment, string sortBy)
    {
        var query = PublishedJoinQuery();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = $"%{keyword.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.QuestionSet.Title ?? "", term) ||
                (x.QuestionSet.HrNote != null
                    && !EF.Functions.ILike(x.QuestionSet.HrNote, MarketplaceDescriptionHelper.StudioSavePrefix + "%")
                    && !EF.Functions.ILike(x.QuestionSet.HrNote, MarketplaceDescriptionHelper.StudioMirrorPrefix + "%")
                    && EF.Functions.ILike(x.QuestionSet.HrNote, term)) ||
                (x.HrUser != null && (
                    EF.Functions.ILike(x.HrUser.FullName, term) ||
                    EF.Functions.ILike(x.HrUser.Email, term))) ||
                (x.Company != null && EF.Functions.ILike(x.Company.Name, term)));
        }

        if (companyId.HasValue)
            query = query.Where(x => x.Company != null && x.Company.Id == companyId.Value);

        if (hrUserId.HasValue)
            query = query.Where(x => x.QuestionSet.OwnerId == hrUserId.Value);

        // SCRUM-472: lọc theo chế độ Practice / Tuyển
        if (isHiringAssessment.HasValue)
            query = query.Where(x => x.QuestionSet.IsHiringAssessment == isHiringAssessment.Value);

        var projected = ApplySort(ProjectRows(query), sortBy);
        var totalCount = await projected.CountAsync();
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<AdminMarketplaceSetRow?> GetPublishedByIdAsync(Guid id)
        => await ProjectRows(PublishedJoinQuery().Where(x => x.QuestionSet.Id == id))
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<AdminMarketplaceQuestionSummaryDto>> GetQuestionSummariesAsync(Guid questionSetId)
        => await _context.QuestionSetQuestions
            .AsNoTracking()
            .Where(q => q.QuestionSetId == questionSetId && q.IsActive)
            .OrderBy(q => q.Order)
            .Select(q => new AdminMarketplaceQuestionSummaryDto
            {
                Id = q.Id,
                Order = q.Order,
                Question = q.Question,
                QuestionType = q.QuestionType,
                Difficulty = q.Difficulty,
                Skill = q.Skill
            })
            .ToListAsync();

    public async Task<IReadOnlyList<QuestionSetPractitionerRow>> ListPractitionersForAdminAsync(Guid questionSetId)
    {
        // Admin oversight: không lọc AllowRecruiterRecommendation
        return await _context.PracticeSessions
            .AsNoTracking()
            .Where(s => s.QuestionSetId == questionSetId && s.IsActive)
            .Join(_context.Users.AsNoTracking(),
                s => s.CandidateUserId, u => u.Id,
                (s, u) => new { s, u })
            .GroupJoin(_context.CandidateProfiles.AsNoTracking(),
                x => x.s.CandidateUserId, p => p.UserId,
                (x, profiles) => new { x.s, x.u, profiles })
            .SelectMany(
                x => x.profiles.DefaultIfEmpty(),
                (x, p) => new { x.s, x.u, p })
            .OrderByDescending(x => x.s.StartedAt)
            .Select(x => new QuestionSetPractitionerRow
            {
                CandidateUserId = x.s.CandidateUserId,
                CandidateName = x.u.FullName,
                CandidateEmail = x.u.Email,
                TargetRole = x.p != null ? x.p.TargetRole : null,
                SeniorityLevel = x.p != null ? x.p.SeniorityLevel : null,
                Status = x.s.Status,
                OverallScore = x.s.OverallScore,
                StartedAt = x.s.StartedAt,
                CompletedAt = x.s.CompletedAt
            })
            .ToListAsync();
    }

    public Task<QuestionSet?> GetByIdForUpdateAsync(Guid id)
        => _context.QuestionSets.FirstOrDefaultAsync(qs => qs.Id == id && qs.IsActive);

    public async Task UpdateAsync(QuestionSet questionSet)
    {
        questionSet.UpdatedAt = DateTime.UtcNow;
        _context.QuestionSets.Update(questionSet);
        await _context.SaveChangesAsync();
    }

    public Task<int> CountPinnedAsync()
        => _context.QuestionSets.CountAsync(qs =>
            qs.IsActive
            && qs.Status == QuestionSetStatus.Published
            && qs.Kind == QuestionSetKind.Marketplace
            && qs.IsPinned);

    public async Task<AdminMarketplaceStatsDto> GetStatsAsync()
    {
        var since = DateTime.UtcNow.AddDays(-7);

        var totalPublished = await MarketplacePublishedQuery().CountAsync();

        // Phiên trên bộ Marketplace (kể cả đã unpublish) — loại AI Coaching Personal
        var practicesLast7Days = await (
            from ps in _context.PracticeSessions.AsNoTracking()
            join qs in _context.QuestionSets.AsNoTracking() on ps.QuestionSetId equals qs.Id
            where ps.IsActive
                  && ps.StartedAt >= since
                  && qs.Kind == QuestionSetKind.Marketplace
            select ps
        ).CountAsync();

        var pinnedCount = await CountPinnedAsync();

        var publishedSetIds = MarketplacePublishedQuery().Select(qs => qs.Id);

        // TopHrs: giữ INNER JOIN — chỉ HR có profile + company thật
        var topHrs = await MarketplacePublishedQuery()
            .GroupBy(qs => qs.OwnerId)
            .Select(g => new
            {
                HrUserId = g.Key,
                PublishedSetCount = g.Count(),
                AttemptCount = _context.PracticeSessions.Count(ps =>
                    ps.IsActive && g.Select(x => x.Id).Contains(ps.QuestionSetId))
            })
            .OrderByDescending(x => x.AttemptCount)
            .ThenByDescending(x => x.PublishedSetCount)
            .Take(5)
            .Join(_context.Users.AsNoTracking(),
                x => x.HrUserId, u => u.Id,
                (x, u) => new { x, u })
            .Join(_context.HRProfiles.AsNoTracking(),
                x => x.x.HrUserId, hr => hr.UserId,
                (x, hr) => new { x.x, x.u, hr })
            .Join(_context.Companies.AsNoTracking(),
                x => x.hr.CompanyId, c => c.Id,
                (x, c) => new AdminMarketplaceTopHrDto
                {
                    HrUserId = x.x.HrUserId,
                    HrName = x.u.FullName,
                    CompanyName = c.Name,
                    PublishedSetCount = x.x.PublishedSetCount,
                    AttemptCount = x.x.AttemptCount
                })
            .ToListAsync();

        var topSkills = await _context.QuestionSetQuestions
            .AsNoTracking()
            .Where(q => q.IsActive
                        && q.Skill != null
                        && q.Skill != ""
                        && publishedSetIds.Contains(q.QuestionSetId))
            .GroupBy(q => q.Skill!)
            .Select(g => new AdminMarketplaceTopSkillDto
            {
                Skill = g.Key,
                QuestionCount = g.Count()
            })
            .OrderByDescending(x => x.QuestionCount)
            .Take(5)
            .ToListAsync();

        return new AdminMarketplaceStatsDto
        {
            TotalPublished = totalPublished,
            PracticesLast7Days = practicesLast7Days,
            PinnedCount = pinnedCount,
            TopHrs = topHrs,
            TopSkills = topSkills
        };
    }
}
