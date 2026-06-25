using ApplicationLayer.DTOs.Rag;

namespace ApplicationLayer.Interfaces.Services;

public interface IRagService
{
    Task<RagIngestResult> IngestAsync(RagIngestRequest request, CancellationToken ct = default);
    Task<RagAsyncAcceptedResult> EnqueueIngestAsync(RagIngestRequest request, CancellationToken ct = default);
    Task<RagDeleteResult> DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default);
    Task<ParseJdResult> ParseJdAsync(Stream fileStream, string fileName, CancellationToken ct = default);
    Task<ValidateJdResult> ValidateJdAsync(ValidateJdRequest request, CancellationToken ct = default);
    Task<GeneratePlanResult> GeneratePlanAsync(GeneratePlanRequest request, CancellationToken ct = default);
    Task<RagAsyncAcceptedResult> EnqueueGeneratePlanAsync(Guid jobId, GeneratePlanRequest request, CancellationToken ct = default);
    Task<GenerateQuestionsFromPlanResult> GenerateQuestionsFromPlanAsync(GenerateQuestionsFromPlanRequest request, CancellationToken ct = default);
    Task<RagAsyncAcceptedResult> EnqueueGenerateQuestionsFromPlanAsync(Guid jobId, GenerateQuestionsFromPlanRequest request, CancellationToken ct = default);
    Task<RagHealthStatusDto> GetHealthStatusAsync(CancellationToken ct = default);
}
