using System.Text.Json;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Common;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services;

public class QuestionGenerationJobInternalService : IQuestionGenerationJobInternalService
{
    private readonly IQuestionGenerationJobRepository _repository;
    private readonly ILogger<QuestionGenerationJobInternalService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public QuestionGenerationJobInternalService(
        IQuestionGenerationJobRepository repository,
        ILogger<QuestionGenerationJobInternalService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<JobStatusResponseDto> UpdateGenerationResultAsync(
        Guid jobId, QuestionGenerationCallbackDto dto)
    {
        var job = await _repository.GetByIdWithPlanAndQuestionsAsync(jobId)
            ?? throw new NotFoundException("Question generation job không tồn tại.");

        var phase = dto.Phase?.ToUpperInvariant() ?? string.Empty;

        if (phase == "PLAN")
            await ApplyPlanResultAsync(job, dto);
        else if (phase == "QUESTIONS")
            await ApplyQuestionsResultAsync(job, dto);
        else
            throw new BadRequestException($"Phase callback không hợp lệ: {dto.Phase}");

        await _repository.UpdateAsync(job);

        return new JobStatusResponseDto
        {
            JobId = job.Id,
            Status = job.Status
        };
    }

    private async Task ApplyPlanResultAsync(QuestionGenerationJob job, QuestionGenerationCallbackDto dto)
    {
        if (dto.Success)
        {
            if (dto.Plan is null)
            {
                _logger.LogWarning("Callback PLAN success nhưng thiếu plan cho job {JobId}", job.Id);
                job.Status = QuestionGenerationJobStatus.Failed;
                job.ErrorMessage = JobErrorSerializer.Serialize(new StructuredErrorPayload
                {
                    Error = "Lỗi sinh plan",
                    Detail = "RAG callback thiếu plan.",
                    Stage = ErrorStage.PlanGeneration,
                    Source = "RAG",
                    Errors = ["RAG callback thiếu plan."]
                });
                return;
            }

            var planJson = JsonSerializer.Serialize(dto.Plan, JsonOptions);

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
            return;
        }

        job.Status = QuestionGenerationJobStatus.Failed;
        job.ErrorMessage = SerializeCallbackError(dto, ErrorStage.PlanGeneration, "Lỗi sinh plan");
    }

    private async Task ApplyQuestionsResultAsync(QuestionGenerationJob job, QuestionGenerationCallbackDto dto)
    {
        if (dto.Success)
        {
            var questions = dto.Questions ?? new List<RagGeneratedQuestionDto>();
            await _repository.DeleteQuestionsByJobIdAsync(job.Id);

            var entities = questions.Select((q, index) => new GeneratedQuestion
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
                EvaluationCriteriaJson = JsonSerializer.Serialize(
                    q.EvaluationCriteria ?? new List<string>(), JsonOptions),
                CitationsJson = JsonSerializer.Serialize(
                    q.Citations ?? new List<object>(), JsonOptions)
            }).ToList();

            if (entities.Count > 0)
                await _repository.AddQuestionsAsync(entities);

            job.Status = QuestionGenerationJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = null;
            return;
        }

        job.Status = QuestionGenerationJobStatus.Failed;
        job.ErrorMessage = SerializeCallbackError(dto, ErrorStage.QuestionGeneration, "Lỗi sinh câu hỏi");
    }

    private static string SerializeCallbackError(
        QuestionGenerationCallbackDto dto, string defaultStage, string defaultError)
    {
        var errors = dto.Errors ?? [];
        if (errors.Count == 0 && !string.IsNullOrWhiteSpace(dto.Detail))
            errors = [dto.Detail];
        if (errors.Count == 0 && !string.IsNullOrWhiteSpace(dto.Error))
            errors = [dto.Error];

        return JobErrorSerializer.Serialize(new StructuredErrorPayload
        {
            Error = dto.Error ?? defaultError,
            Detail = dto.Detail ?? dto.Error ?? defaultError,
            Stage = dto.Stage ?? defaultStage,
            Source = dto.Source ?? "RAG",
            Errors = errors.Count > 0 ? errors : [defaultError]
        });
    }
}
