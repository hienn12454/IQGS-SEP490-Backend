using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Settings;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Jobs;

/// <summary>V1 job pipeline retired — no-op watchdog (tables dropped Phase 4).</summary>
public class StuckQuestionGenerationWatchdogJob : IStuckQuestionGenerationWatchdogJob
{
    private readonly ILogger<StuckQuestionGenerationWatchdogJob> _logger;

    public StuckQuestionGenerationWatchdogJob(
        IQuestionGenerationJobRepository repository,
        IOptions<WatchdogSettings> settings,
        ILogger<StuckQuestionGenerationWatchdogJob> logger)
    {
        _ = repository;
        _ = settings;
        _logger = logger;
    }

    [Queue("default")]
    public Task ExecuteAsync()
    {
        _logger.LogDebug("StuckQuestionGenerationWatchdogJob skipped — V1 generate retired.");
        return Task.CompletedTask;
    }
}
