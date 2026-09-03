using ApplicationLayer.DTOs.Rag;

namespace ApplicationLayer.Interfaces.Services;

public interface IRagService
{
    Task<RagIngestResult> IngestAsync(RagIngestRequest request, CancellationToken ct = default);
    Task<RagAsyncAcceptedResult> EnqueueIngestAsync(RagIngestRequest request, CancellationToken ct = default);
    Task<RagDeleteResult> DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default);
    Task<ParseJdResult> ParseJdAsync(Stream fileStream, string fileName, CancellationToken ct = default);
    Task<ParseCvResult> ParseCvAsync(Stream fileStream, string fileName, CancellationToken ct = default);
    Task<ValidateJdResult> ValidateJdAsync(ValidateJdRequest request, CancellationToken ct = default);
    /// <summary>SCRUM-416: LLM extract position/role/seniority/language/skills từ JD text.</summary>
    Task<AnalyzeJdResult> AnalyzeJdAsync(AnalyzeJdRequest request, CancellationToken ct = default);
    Task<RecommendInterviewConfigurationResult> RecommendInterviewConfigurationAsync(
        RecommendInterviewConfigurationRequest request, CancellationToken ct = default);
    Task<GeneratePlanResult> GeneratePlanAsync(GeneratePlanRequest request, CancellationToken ct = default);
    /// <summary>SCRUM-420: Refine plan — trả PlanPatch delta.</summary>
    Task<RefinePlanResult> RefinePlanAsync(RefinePlanRequest request, CancellationToken ct = default);
    /// <summary>SCRUM-426: khóa/rebind citations trên outline (Apply Live Preview).</summary>
    Task<BindOutlineSourcesResult> BindOutlineSourcesAsync(BindOutlineSourcesRequest request, CancellationToken ct = default);
    Task<GenerateQuestionsFromPlanResult> GenerateQuestionsAsync(GeneratePlanRequest request, CancellationToken ct = default);
    Task<RagAsyncAcceptedResult> EnqueueGeneratePlanAsync(Guid jobId, GeneratePlanRequest request, CancellationToken ct = default);
    Task<GenerateQuestionsFromPlanResult> GenerateQuestionsFromPlanAsync(GenerateQuestionsFromPlanRequest request, CancellationToken ct = default);
    Task<GeneratePlanResult> GenerateCandidatePlanAsync(GeneratePlanRequest request, CancellationToken ct = default);
    Task<GenerateQuestionsFromPlanResult> GenerateCandidateQuestionsFromPlanAsync(GenerateQuestionsFromPlanRequest request, CancellationToken ct = default);
    Task<RagAsyncAcceptedResult> EnqueueGenerateQuestionsFromPlanAsync(Guid jobId, GenerateQuestionsFromPlanRequest request, CancellationToken ct = default);
    Task<QuestionAssistResult> AskQuestionAssistAsync(QuestionAssistRequest request, CancellationToken ct = default);
    Task<EvaluateAnswerResult> EvaluateAnswerAsync(EvaluateAnswerRequest request, CancellationToken ct = default);
    Task<EvaluateQuestionSetResult> EvaluateQuestionSetAsync(EvaluateQuestionSetRequest request, CancellationToken ct = default);
    Task<PracticeSessionInsightResult> GeneratePracticeSessionInsightAsync(PracticeSessionInsightRequest request, CancellationToken ct = default);
    Task<RagHealthStatusDto> GetHealthStatusAsync(CancellationToken ct = default);

    /// <summary>SCRUM-443/444: retrieve SYSTEM + HR (HR chỉ khi DocumentIds non-empty).</summary>
    Task<RagRetrieveResult> RetrieveAsync(RagRetrieveRequest request, CancellationToken ct = default);
}
