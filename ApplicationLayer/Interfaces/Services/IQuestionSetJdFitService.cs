using ApplicationLayer.DTOs.Rag;

namespace ApplicationLayer.Interfaces.Services;

public interface IQuestionSetJdFitService
{
    Task<JdFitReviewResponse> GetAsync(Guid questionSetId, Guid ownerId, CancellationToken ct = default);
    Task<JdFitReviewResponse> ReviewAsync(Guid questionSetId, Guid ownerId, CancellationToken ct = default);
}
