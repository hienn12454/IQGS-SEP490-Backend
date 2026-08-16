using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services;

public class CandidateQuestionSetService : ICandidateQuestionSetService
{
    private static readonly HashSet<string> AllowedSortBy = new(StringComparer.OrdinalIgnoreCase)
    {
        "featured", "newest", "most_practiced", "highest_rated", "best_match"
    };

    private readonly ICandidateMarketplaceRepository _repository;
    private readonly IPlatformSettingsRepository _platformSettingsRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly ICandidateProfileRepository _profiles;
    private readonly IPracticeSessionRepository _sessions;
    private readonly ILogger<CandidateQuestionSetService> _logger;

    public CandidateQuestionSetService(
        ICandidateMarketplaceRepository repository,
        IPlatformSettingsRepository platformSettingsRepository,
        IBlobStorageService blobStorage,
        ICandidateProfileRepository profiles,
        IPracticeSessionRepository sessions,
        ILogger<CandidateQuestionSetService> logger)
    {
        _repository = repository;
        _platformSettingsRepository = platformSettingsRepository;
        _blobStorage = blobStorage;
        _profiles = profiles;
        _sessions = sessions;
        _logger = logger;
    }

    public async Task<PagedResultDto<CandidateQuestionSetListItemDto>> ListPublishedAsync(
        CandidateQuestionSetListQueryDto query, Guid? candidateUserId)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var sortBy = NormalizeSortBy(query.SortBy);

        string? targetRole = null;
        IReadOnlyList<string>? overlap = null;
        IReadOnlyList<string> cvSkills = Array.Empty<string>();
        CandidateProfileSkills? profileSkills = null;
        int? minAttemptCount = null;
        bool? hasCompletedPractice = null;
        var trendingThreshold = (await _platformSettingsRepository.GetAsync()).MinAttemptsForTrending;

        var chip = (query.Chip ?? "").Trim().ToLowerInvariant();
        if (chip is "trending")
            minAttemptCount = trendingThreshold;

        if (candidateUserId is Guid uid)
        {
            profileSkills = await LoadProfileSkillsAsync(uid);
            cvSkills = profileSkills.CvSkills;
            if (chip is "cv")
                overlap = cvSkills;
            else if (chip is "targetrole" or "target_role")
                targetRole = profileSkills.TargetRole;
            else if (chip is "weak")
            {
                var weak = await ResolveWeakSkillsAsync(uid);
                if (weak.Count > 0)
                    query.Skills = (query.Skills ?? new List<string>()).Concat(weak).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            else if (chip is "unattempted" or "not_attempted")
                hasCompletedPractice = false;
            else if (chip is "retry" or "attempted")
                hasCompletedPractice = true;
        }

        if (sortBy == "best_match" && candidateUserId is null)
            sortBy = "featured";

        var fetchPage = sortBy == "best_match" ? 1 : page;
        var fetchSize = sortBy == "best_match" ? 500 : pageSize;

        var (rows, totalCount) = await _repository.ListPublishedAsync(
            fetchPage, fetchSize, query.Keyword, query.CompanyId, query.Difficulty, query.Skills, sortBy,
            targetRole, overlap, minAttemptCount, candidateUserId, hasCompletedPractice);
        var items = rows.Select(r => PublishedQuestionSetMapper.ToListItemDto(r, trendingThreshold)).ToList();

        await EnrichListItemsAsync(items, candidateUserId, cvSkills);

        if (sortBy == "best_match")
        {
            items = items
                .OrderByDescending(i => i.MatchPercent ?? -1)
                .ThenByDescending(i => i.AttemptCount)
                .ToList();
            totalCount = items.Count;
            items = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        return new PagedResultDto<CandidateQuestionSetListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CandidateQuestionSetDetailDto> GetPublishedByIdAsync(Guid id, Guid candidateUserId)
    {
        var detail = await _repository.GetPublishedByIdAsync(id)
            ?? await _repository.GetPersonalByIdForOwnerAsync(id, candidateUserId)
            ?? throw new NotFoundException("Bộ câu hỏi không tồn tại hoặc chưa được publish.");

        var trendingThreshold = (await _platformSettingsRepository.GetAsync()).MinAttemptsForTrending;
        var ordered = detail.Questions.OrderBy(q => q.Order).ToList();

        var questions = ordered.Select(q => new CandidateQuestionItemDto
        {
            Id = q.Id,
            Order = q.Order,
            Question = string.Empty,
            QuestionType = q.QuestionType,
            Difficulty = q.Difficulty,
            Skill = q.Skill,
            FocusArea = q.FocusArea,
            Rationale = null,
            Citations = new List<object>(),
            CodeTemplateType = null,
            CodeSnippet = null,
            AttachedImageUrl = null,
            AnswerMethod = AnswerMethodNormalizer.Resolve(q.AnswerMethod, null, null),
            IsLocked = false
        }).ToList();

        return new CandidateQuestionSetDetailDto
        {
            Id = detail.Id,
            Title = PublishedQuestionSetMapper.ResolveTitle(detail.Title, detail.CompanyName),
            CompanyId = detail.CompanyId,
            CompanyName = detail.CompanyName,
            CompanyLogo = CompanyLogoResolver.Resolve(detail.CompanyLogo, detail.CompanyWebsite, detail.CompanyName),
            Description = detail.Description,
            Difficulty = detail.Difficulty,
            Skills = PublishedQuestionSetMapper.MergeSkills(
                ordered.Where(q => !string.IsNullOrWhiteSpace(q.Skill)).Select(q => q.Skill!),
                detail.SkillsJson),
            TotalQuestions = ordered.Count,
            VisibleQuestionCount = ordered.Count,
            EstimatedTimeMinutes = detail.TimeLimitMinutes
                ?? ordered.Count * PublishedQuestionSetMapper.EstimatedMinutesPerQuestion,
            TimeLimitMinutes = detail.TimeLimitMinutes,
            Rating = PublishedQuestionSetMapper.RoundRating(detail.Rating),
            AttemptCount = detail.AttemptCount,
            IsPinned = detail.IsPinned,
            IsTrending = detail.AttemptCount >= trendingThreshold,
            Questions = questions
        };
    }

    private async Task EnrichListItemsAsync(
        List<CandidateQuestionSetListItemDto> items, Guid? candidateUserId, IReadOnlyList<string> cvSkills)
    {
        if (items.Count == 0)
            return;

        var ids = items.Select(i => i.Id).ToList();
        var avgs = (await _sessions.ListAverageCompletionMinutesAsync(ids))
            .ToDictionary(x => x.QuestionSetId, x => x.AvgCompletionMinutes);

        Dictionary<Guid, SetLastScoreDto>? last = null;
        if (candidateUserId is Guid uid)
            last = (await _sessions.ListLatestCompletedScoresAsync(uid, ids)).ToDictionary(x => x.QuestionSetId);

        foreach (var item in items)
        {
            if (avgs.TryGetValue(item.Id, out var mins))
                item.AvgCompletionMinutes = mins;

            if (cvSkills.Count > 0)
            {
                var match = SkillMatchHelper.Compute(item.Skills, cvSkills);
                item.MatchPercent = match.Percent;
                item.MatchedSkills = match.Matched;
                item.MissingSkills = match.Missing;
            }

            if (last is not null && last.TryGetValue(item.Id, out var score))
            {
                item.MyLastScore = score.Score;
                item.MyLastCompletedAt = score.CompletedAt;
            }
        }
    }

    private async Task<CandidateProfileSkills> LoadProfileSkillsAsync(Guid candidateUserId)
    {
        var profile = await _profiles.GetByUserIdAsync(candidateUserId);
        return new CandidateProfileSkills(
            SkillMatchHelper.UnionCvSkills(profile?.TechStack, profile?.CvEvaluationJson).ToList(),
            profile?.TargetRole);
    }

    private async Task<List<string>> ResolveWeakSkillsAsync(Guid candidateUserId)
    {
        var stats = await _sessions.ListSkillStatsAsync(candidateUserId);
        return stats.Where(s => s.AverageScore < 70).Select(s => s.Skill).ToList();
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        var value = string.IsNullOrWhiteSpace(sortBy) ? "featured" : sortBy.Trim();
        if (!AllowedSortBy.Contains(value))
            throw new BadRequestException(
                "sortBy không hợp lệ. Cho phép: featured, newest, most_practiced, highest_rated, best_match.");
        return value.ToLowerInvariant();
    }

    private sealed record CandidateProfileSkills(List<string> CvSkills, string? TargetRole);
}
