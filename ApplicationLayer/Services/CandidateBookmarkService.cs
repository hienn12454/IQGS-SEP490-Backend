using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class CandidateBookmarkService : ICandidateBookmarkService
{
    private readonly IQuestionSetBookmarkRepository _bookmarkRepository;
    private readonly ICandidateMarketplaceRepository _marketplaceRepository;

    public CandidateBookmarkService(
        IQuestionSetBookmarkRepository bookmarkRepository,
        ICandidateMarketplaceRepository marketplaceRepository)
    {
        _bookmarkRepository = bookmarkRepository;
        _marketplaceRepository = marketplaceRepository;
    }

    public async Task<BookmarkToggleResponseDto> ToggleAsync(Guid questionSetId, Guid candidateUserId)
    {
        if (!await _marketplaceRepository.IsPublishedAsync(questionSetId))
            throw new NotFoundException("Bộ câu hỏi không tồn tại hoặc chưa được publish.");

        var existing = await _bookmarkRepository.GetAsync(candidateUserId, questionSetId);
        if (existing is not null)
        {
            await _bookmarkRepository.DeleteAsync(existing);
            return new BookmarkToggleResponseDto { QuestionSetId = questionSetId, Bookmarked = false };
        }

        await _bookmarkRepository.AddAsync(new QuestionSetBookmark
        {
            CandidateUserId = candidateUserId,
            QuestionSetId = questionSetId
        });

        return new BookmarkToggleResponseDto { QuestionSetId = questionSetId, Bookmarked = true };
    }

    public async Task<IReadOnlyList<CandidateQuestionSetListItemDto>> ListBookmarkedAsync(Guid candidateUserId)
    {
        var rows = await _marketplaceRepository.ListBookmarkedAsync(candidateUserId);
        return rows.Select(PublishedQuestionSetMapper.ToListItemDto).ToList();
    }
}
