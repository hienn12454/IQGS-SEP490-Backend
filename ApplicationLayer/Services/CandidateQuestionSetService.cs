using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class CandidateQuestionSetService : ICandidateQuestionSetService
{
    private readonly ICandidateMarketplaceRepository _repository;

    public CandidateQuestionSetService(ICandidateMarketplaceRepository repository)
    {
        _repository = repository;
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

    public async Task<CandidateQuestionSetDetailDto> GetPublishedByIdAsync(Guid id)
    {
        var detail = await _repository.GetPublishedByIdAsync(id)
            ?? throw new NotFoundException("Bộ câu hỏi không tồn tại hoặc chưa được publish.");

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
                detail.Questions.Where(q => !string.IsNullOrWhiteSpace(q.Skill)).Select(q => q.Skill!),
                detail.SkillsJson),
            TotalQuestions = detail.Questions.Count,
            EstimatedTimeMinutes = detail.TimeLimitMinutes
                ?? detail.Questions.Count * PublishedQuestionSetMapper.EstimatedMinutesPerQuestion,
            TimeLimitMinutes = detail.TimeLimitMinutes,
            Rating = PublishedQuestionSetMapper.RoundRating(detail.Rating),
            AttemptCount = detail.AttemptCount,
            Questions = detail.Questions.Select(q => new CandidateQuestionItemDto
            {
                Id = q.Id,
                Order = q.Order,
                Question = q.Question,
                QuestionType = q.QuestionType,
                Difficulty = q.Difficulty,
                Skill = q.Skill,
                FocusArea = q.FocusArea,
                Rationale = q.Rationale,
                Citations = PublishedQuestionSetMapper.ParseJsonList<object>(q.CitationsJson)
            }).ToList()
        };
    }
}
