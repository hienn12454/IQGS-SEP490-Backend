using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.DTOs.QuestionGeneration;

namespace ApplicationLayer.Interfaces.Services;

public interface IQuestionSetService
{
    Task<SaveDraftResponseDto> SaveDraftFromJobAsync(Guid jobId, Guid ownerId);
    Task<QuestionSetDetailResponseDto> GetQuestionSetAsync(Guid questionSetId, Guid ownerId);
    Task<IReadOnlyList<QuestionSetListItemDto>> ListQuestionSetsAsync(Guid ownerId, QuestionSetListQueryDto query);
    Task<QuestionSetQuestionResponseDto> UpdateQuestionAsync(
        Guid questionSetId, Guid questionId, Guid ownerId, UpdateQuestionRequestDto dto);
    Task<QuestionSetQuestionResponseDto> AddQuestionAsync(
        Guid questionSetId, Guid ownerId, CreateQuestionRequestDto dto);
    Task DeleteQuestionAsync(Guid questionSetId, Guid questionId, Guid ownerId);
    Task<IReadOnlyList<QuestionSetQuestionResponseDto>> ReorderQuestionsAsync(
        Guid questionSetId, Guid ownerId, ReorderQuestionsRequestDto dto);
}
