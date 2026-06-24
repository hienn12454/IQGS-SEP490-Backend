using System.Text.Json;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Jobs;

public class GenerateQuestionsFromPlanJob : IGenerateQuestionsFromPlanJob
{
    private readonly IQuestionGenerationJobRepository _repository;
    private readonly IRagService _ragService;
    private readonly ILogger<GenerateQuestionsFromPlanJob> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public GenerateQuestionsFromPlanJob(
        IQuestionGenerationJobRepository repository,
        IRagService ragService,
        ILogger<GenerateQuestionsFromPlanJob> logger)
    {
        _repository = repository;
        _ragService = ragService;
        _logger = logger;
    }

    [Queue("question-generation")]
    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await _repository.GetByIdWithPlanAndQuestionsAsync(jobId);
        if (job is null || job.Plan is null || !job.Plan.IsApproved)
        {
            _logger.LogWarning("GenerateQuestionsFromPlanJob: job {JobId} không hợp lệ hoặc plan chưa approve", jobId);
            return;
        }

        job.Status = QuestionGenerationJobStatus.QuestionProcessing;
        await _repository.UpdateAsync(job);

        try
        {
            var approvedPlan = JsonSerializer.Deserialize<object>(job.Plan.PlanJson, JsonOptions)!;

            await _ragService.EnqueueGenerateQuestionsFromPlanAsync(jobId, new GenerateQuestionsFromPlanRequest
            {
                OwnerId = job.OwnerId,
                JobDescription = job.JobDescription,
                ApprovedPlan = approvedPlan,
                HrNote = job.HrNote
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispatch RAG generate-questions async thất bại cho job {JobId}", jobId);
            job.Status = QuestionGenerationJobStatus.Failed;
            job.ErrorMessage = JobErrorSerializer.SerializeFromException(ex);
            await _repository.UpdateAsync(job);
        }
    }
}
