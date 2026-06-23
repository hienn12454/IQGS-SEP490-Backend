using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Jobs;

public class StuckKnowledgeDocumentWatchdogJob : IStuckKnowledgeDocumentWatchdogJob
{
    private readonly IKnowledgeDocumentRepository _repository;
    private readonly WatchdogSettings _settings;
    private readonly ILogger<StuckKnowledgeDocumentWatchdogJob> _logger;

    public StuckKnowledgeDocumentWatchdogJob(
        IKnowledgeDocumentRepository repository,
        IOptions<WatchdogSettings> settings,
        ILogger<StuckKnowledgeDocumentWatchdogJob> logger)
    {
        _repository = repository;
        _settings = settings.Value;
        _logger = logger;
    }

    [Queue("default")]
    public async Task ExecuteAsync()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-_settings.StuckDocumentProcessingMinutes);
        var stuck = await _repository.GetStuckProcessingAsync(threshold);

        foreach (var document in stuck)
        {
            _logger.LogWarning(
                "Document {DocumentId} kẹt PROCESSING > {Minutes} phút — đánh dấu FAILED",
                document.Id,
                _settings.StuckDocumentProcessingMinutes);

            document.Status = KnowledgeDocumentStatus.Failed;
            document.ErrorMessage =
                $"Ingest quá {_settings.StuckDocumentProcessingMinutes} phút. Vui lòng thử reingest.";
            await _repository.UpdateAsync(document);
        }
    }
}
