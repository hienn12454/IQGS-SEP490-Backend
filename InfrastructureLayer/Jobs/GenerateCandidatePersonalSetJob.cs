using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Jobs;

public class GenerateCandidatePersonalSetJob : IGenerateCandidatePersonalSetJob
{
    private readonly ICandidatePersonalSetService _service;
    private readonly ILogger<GenerateCandidatePersonalSetJob> _logger;

    public GenerateCandidatePersonalSetJob(
        ICandidatePersonalSetService service,
        ILogger<GenerateCandidatePersonalSetJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid jobId)
    {
        try
        {
            await _service.ExecuteGenerationAsync(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generate candidate personal set failed job={JobId}", jobId);
        }
    }
}
