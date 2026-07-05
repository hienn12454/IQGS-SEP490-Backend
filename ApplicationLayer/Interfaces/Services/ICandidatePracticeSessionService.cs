using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.Interfaces.Services;

public interface ICandidatePracticeSessionService
{
    Task<PracticeSessionResponseDto> StartAsync(Guid questionSetId, Guid candidateUserId);
    Task<PracticeSessionResponseDto> GetByIdAsync(Guid sessionId, Guid candidateUserId);
    Task<SubmitAnswerResponseDto> SubmitAnswerAsync(Guid sessionId, Guid candidateUserId, SubmitAnswerDto dto);
    Task<PracticeSessionCompleteResponseDto> CompleteAsync(Guid sessionId, Guid candidateUserId);
    Task<PracticeSessionResponseDto> AbandonAsync(Guid sessionId, Guid candidateUserId);
    Task<PracticeSessionFeedbackDto> GetFeedbackAsync(Guid sessionId, Guid candidateUserId);
    Task<PagedResultDto<PracticeSessionListItemDto>> ListAsync(Guid candidateUserId, PracticeSessionListQueryDto query);
    Task<PracticeSessionStatsDto> GetStatsAsync(Guid candidateUserId, PracticeSessionStatsQueryDto query);
}
