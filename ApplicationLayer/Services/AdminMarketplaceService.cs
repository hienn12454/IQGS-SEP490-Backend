using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;
using DomainLayer.Constants;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

/// <summary>SCRUM-404: quản lý Marketplace phía Admin — list/detail/pin + KPI.</summary>
public class AdminMarketplaceService : IAdminMarketplaceService
{
    private static readonly HashSet<string> AllowedSortBy = new(StringComparer.OrdinalIgnoreCase)
    {
        "featured", "newest", "most_practiced", "highest_rated"
    };

    private readonly IAdminMarketplaceRepository _repository;
    private readonly IPlatformSettingsRepository _platformSettingsRepository;

    public AdminMarketplaceService(
        IAdminMarketplaceRepository repository,
        IPlatformSettingsRepository platformSettingsRepository)
    {
        _repository = repository;
        _platformSettingsRepository = platformSettingsRepository;
    }

    public async Task<PagedResultDto<AdminMarketplaceListItemDto>> ListAsync(AdminMarketplaceListQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var sortBy = NormalizeSortBy(query.SortBy);

        var (rows, totalCount) = await _repository.ListPublishedAsync(
            page, pageSize, query.Keyword, query.CompanyId, query.HrUserId, sortBy);

        var trendingThreshold = (await _platformSettingsRepository.GetAsync()).MinAttemptsForTrending;

        return new PagedResultDto<AdminMarketplaceListItemDto>
        {
            Items = rows.Select(r => MapListItem(r, trendingThreshold)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminMarketplaceDetailDto> GetByIdAsync(Guid id)
    {
        var row = await _repository.GetPublishedByIdAsync(id)
            ?? throw new NotFoundException("Bộ câu hỏi không tồn tại hoặc chưa được publish.");

        var questions = await _repository.GetQuestionSummariesAsync(id);
        var practitioners = await _repository.ListPractitionersForAdminAsync(id);

        return new AdminMarketplaceDetailDto
        {
            Id = row.Id,
            Title = PublishedQuestionSetMapper.ResolveTitle(row.Title, row.CompanyName),
            Description = row.Description,
            HrUserId = row.HrUserId,
            HrName = row.HrName,
            HrEmail = row.HrEmail,
            CompanyId = row.CompanyId,
            CompanyName = row.CompanyName,
            CompanyLogo = CompanyLogoResolver.Resolve(row.CompanyLogo, row.CompanyWebsite, row.CompanyName),
            TotalQuestions = row.TotalQuestions,
            AttemptCount = row.AttemptCount,
            UniqueCandidateCount = row.UniqueCandidateCount,
            Rating = PublishedQuestionSetMapper.RoundRating(row.Rating),
            IsPinned = row.IsPinned,
            PinnedAt = row.PinnedAt,
            PublishedAt = row.PublishedAt,
            TimeLimitMinutes = row.TimeLimitMinutes,
            Questions = questions.ToList(),
            Practitioners = practitioners.Select(p => new QuestionSetPractitionerDto
            {
                SessionId = p.SessionId,
                CandidateUserId = p.CandidateUserId,
                CandidateName = p.CandidateName,
                CandidateEmail = p.CandidateEmail,
                TargetRole = p.TargetRole,
                SeniorityLevel = p.SeniorityLevel,
                Status = p.Status,
                OverallScore = p.OverallScore,
                StartedAt = p.StartedAt,
                CompletedAt = p.CompletedAt
            }).ToList()
        };
    }

    public async Task<AdminMarketplacePinResultDto> PinAsync(Guid id)
    {
        var questionSet = await _repository.GetByIdForUpdateAsync(id)
            ?? throw new NotFoundException("Bộ câu hỏi không tồn tại.");

        if (questionSet.Status != QuestionSetStatus.Published)
            throw new ConflictException("Chỉ được ghim bộ câu hỏi đang PUBLISHED trên Marketplace.");

        if (questionSet.IsPinned)
            return new AdminMarketplacePinResultDto
            {
                QuestionSetId = questionSet.Id,
                IsPinned = true,
                PinnedAt = questionSet.PinnedAt
            };

        var settings = await _platformSettingsRepository.GetAsync();
        var pinnedCount = await _repository.CountPinnedAsync();
        if (pinnedCount >= settings.MaxPinnedSets)
            throw new ConflictException(
                $"Đã đạt giới hạn ghim ({settings.MaxPinnedSets} bộ). Bỏ ghim một bộ khác trước.");

        questionSet.IsPinned = true;
        questionSet.PinnedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(questionSet);

        return new AdminMarketplacePinResultDto
        {
            QuestionSetId = questionSet.Id,
            IsPinned = true,
            PinnedAt = questionSet.PinnedAt
        };
    }

    public async Task<AdminMarketplacePinResultDto> UnpinAsync(Guid id)
    {
        var questionSet = await _repository.GetByIdForUpdateAsync(id)
            ?? throw new NotFoundException("Bộ câu hỏi không tồn tại.");

        if (!questionSet.IsPinned)
            return new AdminMarketplacePinResultDto
            {
                QuestionSetId = questionSet.Id,
                IsPinned = false,
                PinnedAt = null
            };

        questionSet.IsPinned = false;
        questionSet.PinnedAt = null;
        await _repository.UpdateAsync(questionSet);

        return new AdminMarketplacePinResultDto
        {
            QuestionSetId = questionSet.Id,
            IsPinned = false,
            PinnedAt = null
        };
    }

    public Task<AdminMarketplaceStatsDto> GetStatsAsync()
        => _repository.GetStatsAsync();

    private static string NormalizeSortBy(string? sortBy)
    {
        var value = string.IsNullOrWhiteSpace(sortBy) ? "featured" : sortBy.Trim();
        if (!AllowedSortBy.Contains(value))
            throw new BadRequestException(
                "sortBy không hợp lệ. Cho phép: featured, newest, most_practiced, highest_rated.");
        return value.ToLowerInvariant();
    }

    private static AdminMarketplaceListItemDto MapListItem(AdminMarketplaceSetRow row, int minAttemptsForTrending) => new()
    {
        Id = row.Id,
        Title = PublishedQuestionSetMapper.ResolveTitle(row.Title, row.CompanyName),
        Description = row.Description,
        HrUserId = row.HrUserId,
        HrName = row.HrName,
        HrEmail = row.HrEmail,
        CompanyId = row.CompanyId,
        CompanyName = row.CompanyName,
        CompanyLogo = CompanyLogoResolver.Resolve(row.CompanyLogo, row.CompanyWebsite, row.CompanyName),
        Difficulty = NormalizeDifficultyLabel(row.Difficulty),
        Skills = row.Skills,
        TotalQuestions = row.TotalQuestions,
        EstimatedTimeMinutes = row.TimeLimitMinutes
            ?? row.TotalQuestions * PublishedQuestionSetMapper.EstimatedMinutesPerQuestion,
        TimeLimitMinutes = row.TimeLimitMinutes,
        AttemptCount = row.AttemptCount,
        UniqueCandidateCount = row.UniqueCandidateCount,
        Rating = PublishedQuestionSetMapper.RoundRating(row.Rating),
        IsPinned = row.IsPinned,
        IsTrending = row.AttemptCount >= minAttemptsForTrending,
        PinnedAt = row.PinnedAt,
        PublishedAt = row.PublishedAt
    };

    private static string NormalizeDifficultyLabel(string? raw)
    {
        var v = (raw ?? "").Trim().ToLowerInvariant();
        if (v == "easy") return "Easy";
        if (v == "hard") return "Hard";
        return "Medium";
    }
}
