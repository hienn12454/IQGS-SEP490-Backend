using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.Interfaces.Services;

public interface ICandidateBookmarkService
{
    Task<BookmarkToggleResponseDto> ToggleAsync(Guid questionSetId, Guid candidateUserId);
    Task<IReadOnlyList<CandidateQuestionSetListItemDto>> ListBookmarkedAsync(Guid candidateUserId);
}
