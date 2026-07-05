using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.DTOs.Rag;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Services;

public interface IQuestionAiAssistService
{
    Task<AskQuestionAiResponseDto> AskAsync(
        Guid jobId, Guid questionId, Guid ownerId, AskQuestionAiRequestDto dto, CancellationToken ct = default);

    Task<QuestionAiChatHistoryResponseDto> GetChatHistoryAsync(
        Guid jobId, Guid questionId, Guid ownerId, CancellationToken ct = default);
}
