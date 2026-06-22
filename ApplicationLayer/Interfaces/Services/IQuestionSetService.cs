using ApplicationLayer.DTOs.QuestionSet;

namespace ApplicationLayer.Interfaces.Services;

public interface IQuestionSetService
{
    Task<SaveDraftResponseDto> SaveDraftFromJobAsync(Guid jobId, Guid ownerId);
    Task<QuestionSetDetailResponseDto> GetQuestionSetAsync(Guid questionSetId, Guid ownerId);
}
