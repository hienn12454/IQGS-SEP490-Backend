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
            .Where(qs => qs.Status == QuestionSetStatus.Published && qs.IsActive
                         && qs.Kind == QuestionSetKind.Marketplace)
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
            CompanyId = x.Company.Id,
            CompanyName = x.Company.Name,
            CompanyLogo = x.Company.LogoUrl,
            CompanyWebsite = x.Company.WebsiteUrl,
            // Studio không còn SourceJob — skill/difficulty lấy từ questions snapshot
            Difficulty = x.QuestionSet.Questions
                .Where(q => q.IsActive && q.Difficulty != null && q.Difficulty != "")
                .Select(q => q.Difficulty)
                .GroupBy(d => d)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "medium",
            SkillsJson = "[]",
            QuestionSkills = x.QuestionSet.Questions
                .Where(q => q.IsActive && q.Skill != null && q.Skill != "")
                .Select(q => q.Skill!)
                .Distinct()
                .OrderBy(s => s)
                .ToList(),
            TotalQuestions = x.QuestionSet.Questions.Count(q => q.IsActive),
            TimeLimitMinutes = x.QuestionSet.TimeLimitMinutes,
            Description = x.QuestionSet.HrNote,
            Rating = _context.PracticeSessions
                .Where(ps => ps.QuestionSetId == x.QuestionSet.Id && ps.IsActive)
                .Average(ps => ps.OverallScore),
            AttemptCount = _context.PracticeSessions
                .Count(ps => ps.QuestionSetId == x.QuestionSet.Id && ps.IsActive),
            IsPinned = x.QuestionSet.IsPinned,
            PinnedAt = x.QuestionSet.PinnedAt,
            PublishedAt = x.QuestionSet.PublishedAt
        });

    private static IQueryable<PublishedQuestionSetRow> ApplySort(
        IQueryable<PublishedQuestionSetRow> projected, string sortBy)
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
            // featured (default): pin trước → PinnedAt → PublishedAt
            _ => projected
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.PinnedAt)
                .ThenByDescending(x => x.PublishedAt)
        };
    }

    public async Task<(IReadOnlyList<PublishedQuestionSetRow> Items, int TotalCount)> ListPublishedAsync(
        int page, int pageSize, string? keyword, Guid? companyId, string? difficulty,
        IReadOnlyList<string>? skills, string sortBy = "featured",
        string? targetRole = null, IReadOnlyList<string>? requireOverlapSkills = null,
        int? minAttemptCount = null, Guid? practiceFilterCandidateId = null, bool? hasCompletedPractice = null)
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

        if (!string.IsNullOrWhiteSpace(targetRole))
        {
            var term = $"%{targetRole.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.QuestionSet.Title ?? "", term) ||
                EF.Functions.ILike(x.QuestionSet.HrNote ?? "", term));
        }

        if (requireOverlapSkills is { Count: > 0 })
        {
            var overlap = requireOverlapSkills;
            query = query.Where(x => x.QuestionSet.Questions.Any(q =>
                q.IsActive && q.Skill != null && overlap.Any(s => EF.Functions.ILike(q.Skill, s))));
        }

        if (practiceFilterCandidateId is Guid cid && hasCompletedPractice is bool completed)
        {
            var completedStatus = PracticeSessionStatus.Completed;
            if (completed)
            {
                query = query.Where(x => _context.PracticeSessions.Any(ps =>
                    ps.QuestionSetId == x.QuestionSet.Id
                    && ps.CandidateUserId == cid
                    && ps.IsActive
                    && ps.Status == completedStatus));
            }
            else
            {
                query = query.Where(x => !_context.PracticeSessions.Any(ps =>
                    ps.QuestionSetId == x.QuestionSet.Id
                    && ps.CandidateUserId == cid
                    && ps.IsActive
                    && ps.Status == completedStatus));
            }
        }

        var projected = ProjectToRow(query);
        if (minAttemptCount is int minAttempts)
            projected = projected.Where(x => x.AttemptCount >= minAttempts);
        projected = ApplySort(projected, sortBy);

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
                CompanyId = x.Company.Id,
                CompanyName = x.Company.Name,
                CompanyLogo = x.Company.LogoUrl,
                CompanyWebsite = x.Company.WebsiteUrl,
                Difficulty = x.QuestionSet.Questions
                    .Where(q => q.IsActive && q.Difficulty != null && q.Difficulty != "")
                    .Select(q => q.Difficulty)
                    .GroupBy(d => d)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault() ?? "medium",
                SkillsJson = "[]",
                TimeLimitMinutes = x.QuestionSet.TimeLimitMinutes,
                Description = x.QuestionSet.HrNote,
                Rating = _context.PracticeSessions
                    .Where(ps => ps.QuestionSetId == x.QuestionSet.Id && ps.IsActive)
                    .Average(ps => ps.OverallScore),
                AttemptCount = _context.PracticeSessions
                    .Count(ps => ps.QuestionSetId == x.QuestionSet.Id && ps.IsActive),
                IsPinned = x.QuestionSet.IsPinned
            }).FirstOrDefaultAsync();

        if (detail is null)
            return null;

        detail.Questions = (await GetQuestionsSnapshotAsync(id)).ToList();
        return detail;
    }

    public Task<bool> IsPublishedAsync(Guid id)
        => _context.QuestionSets.AnyAsync(qs =>
            qs.Id == id
            && qs.Status == QuestionSetStatus.Published
            && qs.IsActive
            && qs.Kind == QuestionSetKind.Marketplace);

    public Task<bool> CanCandidateStartAsync(Guid questionSetId, Guid candidateUserId)
        => _context.QuestionSets.AnyAsync(qs =>
            qs.Id == questionSetId
            && qs.Status == QuestionSetStatus.Published
            && qs.IsActive
            && (qs.Kind == QuestionSetKind.Marketplace
                || (qs.Kind == QuestionSetKind.Personal && qs.OwnerId == candidateUserId)));

    public async Task<PublishedQuestionSetDetail?> GetPersonalByIdForOwnerAsync(Guid id, Guid candidateUserId)
    {
        var set = await _context.QuestionSets.AsNoTracking()
            .FirstOrDefaultAsync(qs =>
                qs.Id == id
                && qs.Kind == QuestionSetKind.Personal
                && qs.OwnerId == candidateUserId
                && qs.IsActive
                && qs.Status == QuestionSetStatus.Published);
        if (set is null)
            return null;

        var skills = await _context.QuestionSetQuestions.AsNoTracking()
            .Where(q => q.QuestionSetId == id && q.IsActive && q.Skill != null && q.Skill != "")
            .Select(q => q.Skill!)
            .Distinct()
            .ToListAsync();

        var difficulty = await _context.QuestionSetQuestions.AsNoTracking()
            .Where(q => q.QuestionSetId == id && q.IsActive && q.Difficulty != null && q.Difficulty != "")
            .GroupBy(q => q.Difficulty)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync() ?? "medium";

        var detail = new PublishedQuestionSetDetail
        {
            Id = set.Id,
            Title = set.Title,
            CompanyId = Guid.Empty,
            CompanyName = "Bộ của tôi",
            Difficulty = difficulty,
            TimeLimitMinutes = set.TimeLimitMinutes,
            Description = set.HrNote,
            AttemptCount = await _context.PracticeSessions.CountAsync(ps => ps.QuestionSetId == id && ps.IsActive),
            Rating = await _context.PracticeSessions
                .Where(ps => ps.QuestionSetId == id && ps.IsActive)
                .AverageAsync(ps => (double?)ps.OverallScore)
        };
        detail.Questions = (await GetQuestionsSnapshotAsync(id)).ToList();
        return detail;
    }

    public async Task<IReadOnlyList<PublishedQuestionSetRow>> ListPersonalByOwnerAsync(Guid candidateUserId)
    {
        var sets = await _context.QuestionSets.AsNoTracking()
            .Where(qs => qs.Kind == QuestionSetKind.Personal && qs.OwnerId == candidateUserId && qs.IsActive)
            .OrderByDescending(qs => qs.CreatedAt)
            .ToListAsync();

        var rows = new List<PublishedQuestionSetRow>();
        foreach (var qs in sets)
        {
            var skills = await _context.QuestionSetQuestions.AsNoTracking()
                .Where(q => q.QuestionSetId == qs.Id && q.IsActive && q.Skill != null && q.Skill != "")
                .Select(q => q.Skill!)
                .Distinct()
                .ToListAsync();
            var total = await _context.QuestionSetQuestions.CountAsync(q => q.QuestionSetId == qs.Id && q.IsActive);
            rows.Add(new PublishedQuestionSetRow
            {
                Id = qs.Id,
                Title = qs.Title,
                CompanyName = "Bộ của tôi",
                Difficulty = "medium",
                QuestionSkills = skills,
                TotalQuestions = total,
                TimeLimitMinutes = qs.TimeLimitMinutes,
                Description = qs.HrNote,
                PublishedAt = qs.PublishedAt ?? qs.CreatedAt
            });
        }
        return rows;
    }

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
                CitationsJson = q.CitationsJson,
                AttachedImageBlobPath = q.AttachedImageBlobPath,
                AnswerMethod = q.AnswerMethod
            })
            .ToListAsync();

    public Task<QuestionEvaluationRubric?> GetQuestionEvaluationRubricAsync(Guid questionSetQuestionId)
        => _context.QuestionSetQuestions
            .AsNoTracking()
            .Where(q => q.Id == questionSetQuestionId && q.IsActive)
            .Select(q => new QuestionEvaluationRubric
            {
                Id = q.Id,
                Question = q.Question,
                QuestionType = q.QuestionType,
                Difficulty = q.Difficulty,
                Skill = q.Skill,
                SampleAnswer = q.SampleAnswer,
                EvaluationCriteriaJson = q.EvaluationCriteriaJson
            })
            .FirstOrDefaultAsync();

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
            .Where(x => bookmarkedIds.Contains(x.QuestionSet.Id));

        return await ApplySort(ProjectToRow(query), "featured").ToListAsync();
    }
}
