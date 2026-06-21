using System.Text.Json;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
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

            var result = await _ragService.GenerateQuestionsFromPlanAsync(new GenerateQuestionsFromPlanRequest
            {
                OwnerId = job.OwnerId,
                JobDescription = job.JobDescription,
                ApprovedPlan = approvedPlan,
                HrNote = job.HrNote
            });

            await _repository.DeleteQuestionsByJobIdAsync(jobId);

            var questions = result.Questions.Select((q, index) => new GeneratedQuestion
            {
                JobId = job.Id,
                Order = q.Order ?? index + 1,
                Question = q.Question,
                QuestionType = q.QuestionType,
                Difficulty = q.Difficulty,
                Skill = q.Skill,
                FocusArea = q.FocusArea,
                Rationale = q.Rationale,
                SampleAnswer = q.SampleAnswer,
                EvaluationCriteriaJson = JsonSerializer.Serialize(q.EvaluationCriteria ?? new List<string>(), JsonOptions),
                CitationsJson = JsonSerializer.Serialize(q.Citations ?? new List<object>(), JsonOptions)
            }).ToList();

            await _repository.AddQuestionsAsync(questions);

            job.Status = QuestionGenerationJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = null;
            await _repository.UpdateAsync(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateQuestionsFromPlanJob thất bại cho job {JobId}", jobId);
            job.Status = QuestionGenerationJobStatus.Failed;
            job.ErrorMessage = JobErrorSerializer.SerializeFromException(ex);
            await _repository.UpdateAsync(job);
        }
    }
}
