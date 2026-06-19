using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Jobs;

public class KnowledgeIngestJob : IKnowledgeIngestJob
{
    private static SemaphoreSlim? _ingestSemaphore;

    private readonly IKnowledgeDocumentRepository _repository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IRagService _ragService;
    private readonly BlobStorageSettings _blobSettings;
    private readonly KnowledgeBaseSettings _kbSettings;
    private readonly ILogger<KnowledgeIngestJob> _logger;

    public KnowledgeIngestJob(
        IKnowledgeDocumentRepository repository,
        IBlobStorageService blobStorage,
        IRagService ragService,
        IOptions<BlobStorageSettings> blobSettings,
        IOptions<KnowledgeBaseSettings> kbSettings,
        ILogger<KnowledgeIngestJob> logger)
    {
        _repository = repository;
        _blobStorage = blobStorage;
        _ragService = ragService;
        _blobSettings = blobSettings.Value;
        _kbSettings = kbSettings.Value;
        _logger = logger;
        _ingestSemaphore ??= new SemaphoreSlim(_kbSettings.MaxConcurrentIngestJobs, _kbSettings.MaxConcurrentIngestJobs);
    }

    [Queue("knowledge-ingestion")]
    public async Task ExecuteAsync(Guid documentId)
    {
        await _ingestSemaphore!.WaitAsync();
        try
        {
            await RunIngestAsync(documentId);
        }
        finally
        {
            _ingestSemaphore.Release();
        }
    }

    private async Task RunIngestAsync(Guid documentId)
    {
        var document = await _repository.GetByIdAsync(documentId);
        if (document is null)
        {
            _logger.LogWarning("KnowledgeIngestJob: document {DocumentId} không tồn tại", documentId);
            return;
        }

        try
        {
            var sasUrl = await _blobStorage.GenerateReadSasUrlAsync(
                document.BlobPath,
                TimeSpan.FromMinutes(_blobSettings.SasExpiryMinutes));

            await _ragService.IngestAsync(new RagIngestRequest
            {
                DocumentId = document.Id,
                BlobReadUrl = sasUrl,
                Scope = document.Scope,
                OwnerId = document.OwnerId,
                FileName = document.FileName,
                SourceTitle = document.SourceTitle
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gọi RAG ingest thất bại cho document {DocumentId}", documentId);
            document.Status = KnowledgeDocumentStatus.Failed;
            document.ErrorMessage = ex.Message;
            await _repository.UpdateAsync(document);
        }
    }
}
