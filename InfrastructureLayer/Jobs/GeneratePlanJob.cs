using System.Text.Json;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Jobs;

public class GeneratePlanJob : IGeneratePlanJob
{
    private readonly IQuestionGenerationJobRepository _repository;
    private readonly IRagService _ragService;
    private readonly ILogger<GeneratePlanJob> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public GeneratePlanJob(
        IQuestionGenerationJobRepository repository,
        IRagService ragService,
        ILogger<GeneratePlanJob> logger)
    {
        _repository = repository;
        _ragService = ragService;
        _logger = logger;
    }

    [Queue("question-generation")]
    public async Task ExecuteAsync(Guid jobId)
    {
        var job = await _repository.GetByIdWithPlanAndQuestionsAsync(jobId);
        if (job is null)
        {
            _logger.LogWarning("GeneratePlanJob: job {JobId} không tồn tại", jobId);
            return;
        }

        job.Status = QuestionGenerationJobStatus.PlanProcessing;
        await _repository.UpdateAsync(job);

        try
        {
            var questionTypes = JsonSerializer.Deserialize<List<string>>(job.QuestionTypesJson, JsonOptions) ?? new();
            var skills = JsonSerializer.Deserialize<List<string>>(job.SkillsJson, JsonOptions) ?? new();

            var result = await _ragService.GeneratePlanAsync(new GeneratePlanRequest
            {
                OwnerId = job.OwnerId,
                JobDescription = job.JobDescription,
                NumberOfQuestions = job.NumberOfQuestions,
                Difficulty = job.Difficulty,
                QuestionTypes = questionTypes,
                Skills = skills,
                HrNote = job.HrNote
            });

            var planJson = JsonSerializer.Serialize(result.Plan, JsonOptions);

            if (job.Plan is null)
            {
                await _repository.AddPlanAsync(new QuestionGenerationPlan
                {
                    JobId = job.Id,
                    PlanJson = planJson
                });
            }
            else
            {
                job.Plan.PlanJson = planJson;
                job.Plan.IsApproved = false;
                job.Plan.ApprovedAt = null;
                await _repository.UpdatePlanAsync(job.Plan);
            }

            job.Status = QuestionGenerationJobStatus.WaitingHrApproval;
            job.ErrorMessage = null;
            await _repository.UpdateAsync(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeneratePlanJob thất bại cho job {JobId}", jobId);
            job.Status = QuestionGenerationJobStatus.Failed;
            job.ErrorMessage = JobErrorSerializer.SerializeFromException(ex);
            await _repository.UpdateAsync(job);
        }
    }
}
