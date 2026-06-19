using System.Text.Json;
using ApplicationLayer.Debugging;
using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services;

public class KnowledgeDocumentService : IKnowledgeDocumentService
{
    private readonly IKnowledgeDocumentRepository _repository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IRagService _ragService;
    private readonly IJobScheduler _jobScheduler;
    private readonly KnowledgeBaseSettings _kbSettings;
    private readonly ILogger<KnowledgeDocumentService> _logger;

    public KnowledgeDocumentService(
        IKnowledgeDocumentRepository repository,
        IBlobStorageService blobStorage,
        IRagService ragService,
        IJobScheduler jobScheduler,
        IOptions<KnowledgeBaseSettings> kbSettings,
        ILogger<KnowledgeDocumentService> logger)
    {
        _repository = repository;
        _blobStorage = blobStorage;
        _ragService = ragService;
        _jobScheduler = jobScheduler;
        _kbSettings = kbSettings.Value;
        _logger = logger;
    }

    public async Task<KnowledgeDocumentResponseDto> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSize,
        KnowledgeDocumentUploadDto dto,
        Guid uploadedBy,
        CancellationToken ct = default)
    {
        ValidateUpload(fileName, fileSize, dto);

        var documentId = Guid.NewGuid();
        var blobPath = BlobPathHelper.BuildBlobPath(dto.Scope, documentId, fileName);

        // #region agent log
        AgentDebugLog.Write("ALL", "KnowledgeDocumentService.UploadAsync", "upload_start",
            new { documentId, scope = dto.Scope, fileName, fileSize });
        // #endregion

        // Tính hash trước khi upload — cần copy stream nếu không seekable
        string? contentHash = null;
        if (fileStream.CanSeek)
        {
            contentHash = await ContentHashHelper.ComputeSha256Async(fileStream, ct);
            fileStream.Position = 0;
        }

        try
        {
            await _blobStorage.UploadAsync(fileStream, contentType, blobPath, ct);
            // #region agent log
            AgentDebugLog.Write("B", "KnowledgeDocumentService.UploadAsync", "blob_upload_ok", new { blobPath });
            // #endregion
        }
        catch (Exception ex)
        {
            // #region agent log
            AgentDebugLog.Write("B", "KnowledgeDocumentService.UploadAsync", "blob_upload_fail",
                new { blobPath, type = ex.GetType().Name, msg = ex.Message });
            // #endregion
            throw WrapStage(ex, "BLOB_UPLOAD");
        }

        var document = new KnowledgeDocument
        {
            Id = documentId,
            Scope = dto.Scope,
            OwnerId = dto.OwnerId,
            FileName = fileName,
            BlobPath = blobPath,
            ContentHash = contentHash,
            SourceTitle = Path.GetFileName(fileName),
            SourceUrl = null,
            Section = null,
            Year = null,
            Status = KnowledgeDocumentStatus.Queued,
            UploadedBy = uploadedBy
        };

        try
        {
            await _repository.AddAsync(document);
            // #region agent log
            AgentDebugLog.Write("A", "KnowledgeDocumentService.UploadAsync", "db_insert_ok", new { documentId });
            // #endregion
        }
        catch (Exception ex)
        {
            // #region agent log
            AgentDebugLog.Write("A", "KnowledgeDocumentService.UploadAsync", "db_insert_fail",
                new { documentId, type = ex.GetType().Name, msg = ex.Message, inner = ex.InnerException?.Message });
            // #endregion
            throw WrapStage(ex, "DB_INSERT");
        }

        try
        {
            _jobScheduler.EnqueueKnowledgeIngest(documentId);
            // #region agent log
            AgentDebugLog.Write("C", "KnowledgeDocumentService.UploadAsync", "hangfire_enqueue_ok", new { documentId });
            // #endregion
        }
        catch (Exception ex)
        {
            // #region agent log
            AgentDebugLog.Write("C", "KnowledgeDocumentService.UploadAsync", "hangfire_enqueue_fail",
                new { documentId, type = ex.GetType().Name, msg = ex.Message });
            // #endregion
            throw WrapStage(ex, "HANGFIRE_ENQUEUE");
        }

        return KnowledgeDocumentInternalService.MapToDto(document);
    }

    private static Exception WrapStage(Exception ex, string stage)
    {
        var root = ex;
        while (root.InnerException is not null)
            root = root.InnerException;

        var wrapped = new Exception($"[{stage}] {root.Message}", ex);
        wrapped.Data["stage"] = stage;
        return wrapped;
    }

    public async Task<PagedResultDto<KnowledgeDocumentResponseDto>> GetPagedAsync(
        KnowledgeDocumentListQueryDto listQuery,
        string? scope = null,
        Guid? ownerId = null)
    {
        var query = new KnowledgeDocumentQueryDto
        {
            Scope = scope,
            OwnerId = ownerId,
            FileName = listQuery.FileName,
            FromDate = listQuery.FromDate,
            ToDate = listQuery.ToDate,
            Page = listQuery.Page,
            PageSize = listQuery.PageSize
        };

        var result = await _repository.GetPagedAsync(query);
        return new PagedResultDto<KnowledgeDocumentResponseDto>
        {
            Items = result.Items.Select(KnowledgeDocumentInternalService.MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<KnowledgeDocumentResponseDto> GetByIdAsync(Guid id, Guid? ownerIdFilter = null)
    {
        var document = await GetDocumentWithOwnership(id, ownerIdFilter);
        return KnowledgeDocumentInternalService.MapToDto(document);
    }

    public async Task<KnowledgeDocumentResponseDto> ReingestAsync(Guid id, Guid? ownerIdFilter = null)
    {
        var document = await GetDocumentWithOwnership(id, ownerIdFilter);
        document.Status = KnowledgeDocumentStatus.Queued;
        document.ErrorMessage = null;
        document.ChunkCount = null;
        await _repository.UpdateAsync(document);
        _jobScheduler.EnqueueKnowledgeIngest(id);
        return KnowledgeDocumentInternalService.MapToDto(document);
    }

    public async Task DeleteAsync(Guid id, Guid? ownerIdFilter = null)
    {
        var document = await GetDocumentWithOwnership(id, ownerIdFilter);

        try
        {
            await _ragService.DeleteDocumentChunksAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG cleanup chunks thất bại cho document {DocumentId}", id);
        }

        try
        {
            await _blobStorage.DeleteAsync(document.BlobPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Xóa blob thất bại cho document {DocumentId}", id);
        }

        await _repository.DeleteHardAsync(document);
    }

    private async Task<KnowledgeDocument> GetDocumentWithOwnership(Guid id, Guid? ownerIdFilter)
    {
        var document = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException("Knowledge document không tồn tại.");

        if (ownerIdFilter.HasValue && document.OwnerId != ownerIdFilter)
            throw new ForbiddenException("Bạn không có quyền truy cập tài liệu này.");

        return document;
    }

    private void ValidateUpload(string fileName, long fileSize, KnowledgeDocumentUploadDto dto)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!_kbSettings.AllowedExtensions.Contains(ext))
            throw new BadRequestException($"Chỉ chấp nhận file: {string.Join(", ", _kbSettings.AllowedExtensions)}.");

        var maxBytes = _kbSettings.MaxFileSizeMb * 1024L * 1024L;
        if (fileSize > maxBytes)
            throw new BadRequestException($"File vượt quá {_kbSettings.MaxFileSizeMb}MB.");

        if (dto.Scope == KnowledgeDocumentScope.System && dto.OwnerId.HasValue)
            throw new BadRequestException("SYSTEM document không được có ownerId.");

        if (dto.Scope == KnowledgeDocumentScope.Hr && !dto.OwnerId.HasValue)
            throw new BadRequestException("HR document bắt buộc có ownerId.");

        if (dto.Scope != KnowledgeDocumentScope.System && dto.Scope != KnowledgeDocumentScope.Hr)
            throw new BadRequestException($"Scope không hợp lệ. Chỉ chấp nhận {KnowledgeDocumentScope.System} hoặc {KnowledgeDocumentScope.Hr}.");
    }
}
