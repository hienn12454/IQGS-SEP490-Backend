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
    private readonly IPlatformSettingsRepository _platformSettingsRepository;

    public CandidateBookmarkService(
        IQuestionSetBookmarkRepository bookmarkRepository,
        ICandidateMarketplaceRepository marketplaceRepository,
        IPlatformSettingsRepository platformSettingsRepository)
    {
        _bookmarkRepository = bookmarkRepository;
        _marketplaceRepository = marketplaceRepository;
        _platformSettingsRepository = platformSettingsRepository;
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
        var trendingThreshold = (await _platformSettingsRepository.GetAsync()).MinAttemptsForTrending;
        return rows.Select(r => PublishedQuestionSetMapper.ToListItemDto(r, trendingThreshold)).ToList();
    }
}
