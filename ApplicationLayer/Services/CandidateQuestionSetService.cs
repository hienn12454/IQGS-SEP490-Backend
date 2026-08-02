using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class CandidateQuestionSetService : ICandidateQuestionSetService
{
    private readonly ICandidateMarketplaceRepository _repository;
    private readonly ISubscriptionGateService _subscriptionGate;

    public CandidateQuestionSetService(
        ICandidateMarketplaceRepository repository,
        ISubscriptionGateService subscriptionGate)
    {
        _repository = repository;
        _subscriptionGate = subscriptionGate;
    }

    public async Task<PagedResultDto<CandidateQuestionSetListItemDto>> ListPublishedAsync(
        CandidateQuestionSetListQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (rows, totalCount) = await _repository.ListPublishedAsync(
            page, pageSize, query.Keyword, query.CompanyId, query.Difficulty, query.Skills);

        return new PagedResultDto<CandidateQuestionSetListItemDto>
        {
            Items = rows.Select(PublishedQuestionSetMapper.ToListItemDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CandidateQuestionSetDetailDto> GetPublishedByIdAsync(Guid id, Guid candidateUserId)
    {
        var detail = await _repository.GetPublishedByIdAsync(id)
            ?? throw new NotFoundException("Bộ câu hỏi không tồn tại hoặc chưa được publish.");

        var ordered = detail.Questions.OrderBy(q => q.Order).ToList();
        var visibleCount = await _subscriptionGate.GetVisibleQuestionCountAsync(candidateUserId, ordered.Count);

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
            Questions = ordered.Select((q, index) =>
            {
                var locked = index >= visibleCount;
                return new CandidateQuestionItemDto
                {
                    Id = q.Id,
                    Order = q.Order,
                    Question = locked ? string.Empty : q.Question,
                    QuestionType = q.QuestionType,
                    Difficulty = q.Difficulty,
                    Skill = locked ? null : q.Skill,
                    FocusArea = locked ? null : q.FocusArea,
                    Rationale = locked ? null : q.Rationale,
                    Citations = locked
                        ? new List<object>()
                        : PublishedQuestionSetMapper.ParseJsonList<object>(q.CitationsJson),
                    IsLocked = locked
                };
            }).ToList()
        };
    }
}
