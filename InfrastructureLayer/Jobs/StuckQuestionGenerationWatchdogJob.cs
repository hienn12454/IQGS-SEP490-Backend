using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Settings;
using DomainLayer.Common;
using DomainLayer.Constants;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Jobs;

public class StuckQuestionGenerationWatchdogJob : IStuckQuestionGenerationWatchdogJob
{
    private static readonly string[] StuckStatuses =
    [
        QuestionGenerationJobStatus.PlanProcessing,
        QuestionGenerationJobStatus.QuestionProcessing
    ];

    private readonly IQuestionGenerationJobRepository _repository;
    private readonly WatchdogSettings _settings;
    private readonly ILogger<StuckQuestionGenerationWatchdogJob> _logger;

    public StuckQuestionGenerationWatchdogJob(
        IQuestionGenerationJobRepository repository,
        IOptions<WatchdogSettings> settings,
        ILogger<StuckQuestionGenerationWatchdogJob> logger)
    {
        _repository = repository;
        _settings = settings.Value;
        _logger = logger;
    }

    [Queue("default")]
    public async Task ExecuteAsync()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-_settings.StuckQuestionGenerationMinutes);
        var stuck = await _repository.GetStuckByStatusesAsync(StuckStatuses, threshold);

        foreach (var job in stuck)
        {
            _logger.LogWarning(
                "Job {JobId} kẹt {Status} > {Minutes} phút — đánh dấu FAILED",
                job.Id,
                job.Status,
                _settings.StuckQuestionGenerationMinutes);

            job.Status = QuestionGenerationJobStatus.Failed;
            job.ErrorMessage = JobErrorSerializer.Serialize(new StructuredErrorPayload
            {
                Error = "Job xử lý quá thời gian chờ",
                Detail = $"Job kẹt {job.Status} quá {_settings.StuckQuestionGenerationMinutes} phút. Dùng retry-plan hoặc retry-questions.",
                Stage = job.Status == QuestionGenerationJobStatus.PlanProcessing
                    ? ErrorStage.PlanGeneration
                    : ErrorStage.QuestionGeneration,
                Source = "BE",
                Errors =
                [
                    $"Job kẹt quá {_settings.StuckQuestionGenerationMinutes} phút. Thử retry tương ứng."
                ]
            });
            await _repository.UpdateAsync(job);
        }
    }
}
