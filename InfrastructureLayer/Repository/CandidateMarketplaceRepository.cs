using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class CandidateMarketplaceRepository : ICandidateMarketplaceRepository
{
    private readonly Database.AppDbContext _context;

    public CandidateMarketplaceRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    /// <summary>Query gốc: question_sets PUBLISHED join HRProfiles/Companies — dùng chung cho list/detail/bookmarks.</summary>
    private IQueryable<PublishedQuestionSetJoin> PublishedWithCompanyQuery()
        => _context.QuestionSets
            .AsNoTracking()
            .Where(qs => qs.Status == QuestionSetStatus.Published && qs.IsActive)
            .Join(_context.HRProfiles.AsNoTracking(),
                qs => qs.OwnerId, hr => hr.UserId,
                (qs, hr) => new { qs, hr })
            .Join(_context.Companies.AsNoTracking(),
                x => x.hr.CompanyId, c => c.Id,
                (x, company) => new PublishedQuestionSetJoin { QuestionSet = x.qs, Company = company });

    private sealed class PublishedQuestionSetJoin
    {
        public QuestionSet QuestionSet { get; set; } = null!;
        public Company Company { get; set; } = null!;
    }

    /// <summary>Chiếu 1 join-row sang read-model card marketplace — dùng chung cho list và bookmarks.</summary>
    private IQueryable<PublishedQuestionSetRow> ProjectToRow(IQueryable<PublishedQuestionSetJoin> query)
        => query.Select(x => new PublishedQuestionSetRow
        {
            Id = x.QuestionSet.Id,
            Title = x.QuestionSet.Title,
            CompanyName = x.Company.Name,
            CompanyLogo = x.Company.LogoUrl,
            Difficulty = x.QuestionSet.SourceJob.Difficulty,
            SkillsJson = x.QuestionSet.SourceJob.SkillsJson,
            TotalQuestions = x.QuestionSet.Questions.Count(q => q.IsActive),
            Description = x.QuestionSet.HrNote,
            Rating = _context.PracticeSessions
                .Where(ps => ps.QuestionSetId == x.QuestionSet.Id && ps.IsActive)
                .Average(ps => ps.OverallScore),
            AttemptCount = _context.PracticeSessions
                .Count(ps => ps.QuestionSetId == x.QuestionSet.Id && ps.IsActive)
        });

    public async Task<(IReadOnlyList<PublishedQuestionSetRow> Items, int TotalCount)> ListPublishedAsync(
        int page, int pageSize, string? keyword, Guid? companyId, string? difficulty, IReadOnlyList<string>? skills)
    {
        var query = PublishedWithCompanyQuery();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = $"%{keyword.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.QuestionSet.Title ?? "", term) ||
                EF.Functions.ILike(x.QuestionSet.HrNote ?? "", term));
        }

        if (companyId.HasValue)
            query = query.Where(x => x.Company.Id == companyId.Value);

        if (!string.IsNullOrWhiteSpace(difficulty))
            query = query.Where(x => x.QuestionSet.Questions.Any(q =>
                q.IsActive && EF.Functions.ILike(q.Difficulty, difficulty)));

        if (skills is { Count: > 0 })
            query = query.Where(x => x.QuestionSet.Questions.Any(q =>
                q.IsActive && q.Skill != null && skills.Any(s => EF.Functions.ILike(q.Skill, s))));

        var projected = ProjectToRow(query.OrderByDescending(x => x.QuestionSet.PublishedAt));

        var totalCount = await projected.CountAsync();
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<PublishedQuestionSetDetail?> GetPublishedByIdAsync(Guid id)
    {
        var detail = await PublishedWithCompanyQuery()
            .Where(x => x.QuestionSet.Id == id)
            .Select(x => new PublishedQuestionSetDetail
            {
                Id = x.QuestionSet.Id,
                Title = x.QuestionSet.Title,
                CompanyName = x.Company.Name,
                CompanyLogo = x.Company.LogoUrl,
                Difficulty = x.QuestionSet.SourceJob.Difficulty,
                SkillsJson = x.QuestionSet.SourceJob.SkillsJson,
                Description = x.QuestionSet.HrNote,
                Rating = _context.PracticeSessions
                    .Where(ps => ps.QuestionSetId == x.QuestionSet.Id && ps.IsActive)
                    .Average(ps => ps.OverallScore),
                AttemptCount = _context.PracticeSessions
                    .Count(ps => ps.QuestionSetId == x.QuestionSet.Id && ps.IsActive)
            }).FirstOrDefaultAsync();

        if (detail is null)
            return null;

        detail.Questions = (await GetQuestionsSnapshotAsync(id)).ToList();
        return detail;
    }

    public Task<bool> IsPublishedAsync(Guid id)
        => _context.QuestionSets.AnyAsync(qs => qs.Id == id && qs.Status == QuestionSetStatus.Published && qs.IsActive);

    public async Task<IReadOnlyList<PublishedQuestionRow>> GetQuestionsSnapshotAsync(Guid questionSetId)
        => await _context.QuestionSetQuestions
            .AsNoTracking()
            .Where(q => q.QuestionSetId == questionSetId && q.IsActive)
            .OrderBy(q => q.Order)
            .Select(q => new PublishedQuestionRow
            {
                Id = q.Id,
                Order = q.Order,
                Question = q.Question,
                QuestionType = q.QuestionType,
                Difficulty = q.Difficulty,
                Skill = q.Skill,
                FocusArea = q.FocusArea,
                Rationale = q.Rationale,
                CitationsJson = q.CitationsJson
            })
            .ToListAsync();

    public Task<bool> QuestionBelongsToSetAsync(Guid questionId, Guid questionSetId)
        => _context.QuestionSetQuestions.AnyAsync(q =>
            q.Id == questionId && q.QuestionSetId == questionSetId && q.IsActive);

    public async Task<IReadOnlyList<PublishedQuestionSetRow>> ListBookmarkedAsync(Guid candidateUserId)
    {
        var bookmarkedIds = _context.QuestionSetBookmarks
            .AsNoTracking()
            .Where(b => b.CandidateUserId == candidateUserId)
            .Select(b => b.QuestionSetId);

        var query = PublishedWithCompanyQuery()
            .Where(x => bookmarkedIds.Contains(x.QuestionSet.Id))
            .OrderByDescending(x => x.QuestionSet.PublishedAt);

        return await ProjectToRow(query).ToListAsync();
    }
}
