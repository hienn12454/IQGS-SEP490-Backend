using ApplicationLayer.DTOs.Hr;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

/// <summary>SCRUM-324: HR bookmark bộ câu hỏi CỦA CHÍNH MÌNH (draft hoặc published) — khác Candidate bookmark (chỉ bộ published của người khác).</summary>
public class HrBookmarkService : IHrBookmarkService
{
    private readonly IHrQuestionSetBookmarkRepository _bookmarkRepository;
    private readonly IQuestionSetRepository _questionSetRepository;
    private readonly IHrCompanyInfoService _hrCompanyInfoService;

    public HrBookmarkService(
        IHrQuestionSetBookmarkRepository bookmarkRepository,
        IQuestionSetRepository questionSetRepository,
        IHrCompanyInfoService hrCompanyInfoService)
    {
        _bookmarkRepository = bookmarkRepository;
        _questionSetRepository = questionSetRepository;
        _hrCompanyInfoService = hrCompanyInfoService;
    }

    public async Task<HrBookmarkToggleResponseDto> ToggleAsync(Guid questionSetId, Guid hrUserId)
    {
        var questionSet = await _questionSetRepository.GetByIdWithQuestionsAsync(questionSetId)
            ?? throw new NotFoundException("Question set không tồn tại.");

        if (questionSet.OwnerId != hrUserId)
            throw new ForbiddenException("Bạn chỉ có thể bookmark bộ câu hỏi của chính mình.");

        var existing = await _bookmarkRepository.GetAsync(hrUserId, questionSetId);
        if (existing is not null)
        {
            await _bookmarkRepository.DeleteAsync(existing);
            return new HrBookmarkToggleResponseDto { QuestionSetId = questionSetId, Bookmarked = false };
        }

        await _bookmarkRepository.AddAsync(new HrQuestionSetBookmark
        {
            HrUserId = hrUserId,
            QuestionSetId = questionSetId
        });

        return new HrBookmarkToggleResponseDto { QuestionSetId = questionSetId, Bookmarked = true };
    }

    public async Task<IReadOnlyList<QuestionSetListItemDto>> ListBookmarkedAsync(Guid hrUserId)
    {
        var questionSets = await _bookmarkRepository.ListBookmarkedQuestionSetsAsync(hrUserId);

        // Chỉ bookmark set của chính mình -> cùng 1 công ty cho mọi item, chỉ cần lookup 1 lần (tránh N+1).
        var (companyName, companyLogo) = await _hrCompanyInfoService.GetByHrUserIdAsync(hrUserId);

        return questionSets.Select(qs => new QuestionSetListItemDto
        {
            QuestionSetId = qs.Id,
            JobId = qs.SourceJobId,
            Title = qs.Title,
            Status = qs.Status,
            CompanyName = companyName,
            CompanyLogo = companyLogo,
            SavedAt = qs.CreatedAt,
            PublishedAt = qs.PublishedAt
        }).ToList();
    }
}
