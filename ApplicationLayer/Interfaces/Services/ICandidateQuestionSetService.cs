using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.Interfaces.Services;

public interface ICandidateQuestionSetService
{
    Task<PagedResultDto<CandidateQuestionSetListItemDto>> ListPublishedAsync(CandidateQuestionSetListQueryDto query);
    Task<CandidateQuestionSetDetailDto> GetPublishedByIdAsync(Guid id, Guid candidateUserId);
}
