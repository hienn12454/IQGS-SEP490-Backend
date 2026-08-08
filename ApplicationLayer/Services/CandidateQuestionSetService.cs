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
        "featured", "newest", "most_practiced", "highest_rated"
    };

    private readonly ICandidateMarketplaceRepository _repository;
    private readonly IPlatformSettingsRepository _platformSettingsRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<CandidateQuestionSetService> _logger;

    public CandidateQuestionSetService(
        ICandidateMarketplaceRepository repository,
        IPlatformSettingsRepository platformSettingsRepository,
        IBlobStorageService blobStorage,
        ILogger<CandidateQuestionSetService> logger)
    {
        _repository = repository;
        _platformSettingsRepository = platformSettingsRepository;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    public async Task<PagedResultDto<CandidateQuestionSetListItemDto>> ListPublishedAsync(
        CandidateQuestionSetListQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var sortBy = NormalizeSortBy(query.SortBy);

        var (rows, totalCount) = await _repository.ListPublishedAsync(
            page, pageSize, query.Keyword, query.CompanyId, query.Difficulty, query.Skills, sortBy);

        var trendingThreshold = (await _platformSettingsRepository.GetAsync()).MinAttemptsForTrending;

        return new PagedResultDto<CandidateQuestionSetListItemDto>
        {
            Items = rows.Select(r => PublishedQuestionSetMapper.ToListItemDto(r, trendingThreshold)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CandidateQuestionSetDetailDto> GetPublishedByIdAsync(Guid id, Guid candidateUserId)
    {
        var detail = await _repository.GetPublishedByIdAsync(id)
            ?? throw new NotFoundException("Bộ câu hỏi không tồn tại hoặc chưa được publish.");

        var trendingThreshold = (await _platformSettingsRepository.GetAsync()).MinAttemptsForTrending;
        var ordered = detail.Questions.OrderBy(q => q.Order).ToList();
        // Teaser Freemium: Free làm full bài — không khóa nội dung câu hỏi giữa phiên
        var visibleCount = ordered.Count;

        var questions = new List<CandidateQuestionItemDto>(ordered.Count);
        foreach (var q in ordered)
        {
            var meta = QuestionRationaleMetaParser.Parse(q.Rationale);
            var imageUrl = await TryResolveImageUrlAsync(q.AttachedImageBlobPath);

            questions.Add(new CandidateQuestionItemDto
            {
                Id = q.Id,
                Order = q.Order,
                Question = q.Question,
                QuestionType = q.QuestionType,
                Difficulty = q.Difficulty,
                Skill = q.Skill,
                FocusArea = q.FocusArea,
                Rationale = meta.CleanRationale,
                Citations = PublishedQuestionSetMapper.ParseJsonList<object>(q.CitationsJson),
                CodeTemplateType = meta.CodeTemplateType,
                CodeSnippet = meta.CodeSnippet,
                AttachedImageUrl = imageUrl,
                AnswerMethod = AnswerMethodNormalizer.Resolve(
                    q.AnswerMethod, meta.CodeTemplateType, meta.CodeSnippet),
                IsLocked = false
            });
        }

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
            VisibleQuestionCount = visibleCount,
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

    private static string NormalizeSortBy(string? sortBy)
    {
        var value = string.IsNullOrWhiteSpace(sortBy) ? "featured" : sortBy.Trim();
        if (!AllowedSortBy.Contains(value))
            throw new BadRequestException(
                "sortBy không hợp lệ. Cho phép: featured, newest, most_practiced, highest_rated.");
        return value.ToLowerInvariant();
    }

    private async Task<string?> TryResolveImageUrlAsync(string? blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
            return null;

        try
        {
            return await _blobStorage.GenerateReadSasUrlAsync(blobPath, TimeSpan.FromHours(2));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không tạo được SAS URL cho ảnh câu hỏi Candidate (blob={BlobPath}).", blobPath);
            return null;
        }
    }
}
