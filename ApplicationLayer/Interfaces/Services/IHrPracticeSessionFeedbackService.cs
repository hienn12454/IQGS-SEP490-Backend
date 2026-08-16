using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.Interfaces.Services;

public interface IHrPracticeSessionFeedbackService
{
    Task<PracticeSessionFeedbackDto> GetFeedbackForHrAsync(Guid sessionId, Guid hrUserId);
}
