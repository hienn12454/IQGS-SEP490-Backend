using ApplicationLayer.DTOs.QuestionGeneration;

namespace ApplicationLayer.Interfaces.Services;

public interface IQuestionGenerationJobInternalService
{
    Task<JobStatusResponseDto> UpdateGenerationResultAsync(Guid jobId, QuestionGenerationCallbackDto dto);
}
