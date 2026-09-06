using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Interfaces;
using DomainLayer.Constants;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using InfrastructureLayer.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Services.Studio;

/// <summary>
/// Studio Sources → RAG: upload y hệt luồng Knowledge Base HR
/// (Blob + knowledge_documents + Hangfire KnowledgeIngestJob → RAG ingest/async).
/// StudioKnowledgeDocument chỉ là lớp UI/selection gắn với KnowledgeDocumentId.
/// SCRUM-373: hỗ trợ gắn doc đã có từ /hr/knowledge (library attach).
/// </summary>
public sealed class StudioKnowledgeRagService(
    AppDbContext dbContext,
    IInterviewProjectService projectService,
    IKnowledgeDocumentService knowledgeDocumentService,
    IRagService ragService) : IStudioKnowledgeDocumentService
{
    /// <summary>Prefix StoragePath khi gắn từ KB — Delete chỉ unlink, không xóa knowledge_documents.</summary>
    private const string LibraryLinkPrefix = "library:";

    public async Task<StudioDocumentDto> UploadAsync(Guid projectId, Guid userId, UploadStudioDocumentRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, requireEdit: true, ct);

        await using var stream = new MemoryStream(request.Content);
        var kbDto = new KnowledgeDocumentUploadDto
        {
            Scope = KnowledgeDocumentScope.Hr,
            OwnerId = userId,
            // SCRUM-442: bắt buộc loại khi upload từ Studio
            DocumentType = request.DocumentType
        };

        // Gọi đúng service upload cũ: blob + QUEUED + Hangfire ingest
        KnowledgeDocumentResponseDto uploaded;
        try
        {
            uploaded = await knowledgeDocumentService.UploadAsync(
                stream,
                request.FileName,
                request.ContentType,
                request.Content.LongLength,
                kbDto,
                userId,
                ct);
        }
        catch (Exception ex) when (ex is not StudioBusinessException)
        {
            throw new StudioBusinessException(
                "KNOWLEDGE_UPLOAD_FAILED",
                StatusCodes.Status502BadGateway,
                $"Upload knowledge base thất bại: {ex.InnerException?.Message ?? ex.Message}");
        }

        var blobPath = BlobPathHelper.BuildBlobPath(KnowledgeDocumentScope.Hr, uploaded.DocumentId, request.FileName);
        // FileType DB max 120 — MIME DOCX dài; fallback extension nếu thiếu content-type
        var fileType = NormalizeFileType(request.ContentType, request.FileName);
        var row = new StudioKnowledgeDocument
        {
            ProjectId = projectId,
            KnowledgeDocumentId = uploaded.DocumentId,
            FileName = request.FileName,
            FileType = fileType,
            FileSize = request.Content.LongLength,
            StoragePath = blobPath,
            // Chưa Completed thì chưa chọn — tránh IsSelected=true khi Queued
            IsSelected = false,
            ProcessingStatus = MapRagStatus(uploaded.Status),
            ProcessingError = uploaded.ErrorMessage
        };

        try
        {
            dbContext.StudioKnowledgeDocuments.Add(row);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException(
                "STUDIO_DOCUMENT_SAVE_FAILED",
                StatusCodes.Status500InternalServerError,
                $"Lưu studio document thất bại (kiểm tra migration KnowledgeDocumentId/FileType): {ex.InnerException?.Message ?? ex.Message}");
        }

        // Nếu user muốn select ngay và RAG đã Completed (hiếm khi sync)
        if (request.IsSelected && row.ProcessingStatus == DocumentProcessingStatus.Completed)
        {
            row.IsSelected = true;
            await dbContext.SaveChangesAsync(ct);
        }

        return Map(row, uploaded.Status, uploaded.ChunkCount, uploaded.ErrorMessage, KnowledgeDocumentScope.Hr, uploaded.DocumentType);
    }

    private static string NormalizeFileType(string? contentType, string fileName)
    {
        var mime = (contentType ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(mime) && mime.Length <= 120 && !string.Equals(mime, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return mime;

        var ext = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(ext))
            return ext.TrimStart('.').ToLowerInvariant();

        return string.IsNullOrWhiteSpace(mime) ? "bin" : mime[..Math.Min(120, mime.Length)];
    }

    public async Task<IReadOnlyList<StudioDocumentDto>> ListAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, requireEdit: false, ct);

        var studioDocs = await dbContext.StudioKnowledgeDocuments
            .Where(x => x.ProjectId == projectId && x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var kbIds = studioDocs.Where(x => x.KnowledgeDocumentId.HasValue).Select(x => x.KnowledgeDocumentId!.Value).ToList();
        var kbMap = await dbContext.KnowledgeDocuments
            .Where(x => kbIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var result = new List<StudioDocumentDto>();
        foreach (var doc in studioDocs)
        {
            string? ragStatus = null;
            int? chunkCount = null;
            string? error = doc.ProcessingError;

            string? scope = KnowledgeDocumentScope.Hr;
            string documentType = KnowledgeDocumentType.Unclassified;
            if (doc.KnowledgeDocumentId is Guid kid && kbMap.TryGetValue(kid, out var kb))
            {
                ragStatus = kb.Status;
                chunkCount = kb.ChunkCount;
                error = kb.ErrorMessage ?? error;
                scope = string.IsNullOrWhiteSpace(kb.Scope) ? KnowledgeDocumentScope.Hr : kb.Scope;
                documentType = KnowledgeDocumentType.FromSection(kb.Section);
                var mapped = MapRagStatus(kb.Status);
                if (doc.ProcessingStatus != mapped || doc.ProcessingError != error)
                {
                    doc.ProcessingStatus = mapped;
                    doc.ProcessingError = error;
                    doc.UpdatedAt = DateTime.UtcNow;
                }
            }

            result.Add(Map(doc, ragStatus, chunkCount, error, scope, documentType));
        }

        await dbContext.SaveChangesAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<StudioLibraryDocumentDto>> ListLibraryAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, requireEdit: false, ct);

        // Doc HR của user — ưu tiên COMPLETED; vẫn trả về status khác để FE disable
        var kbDocs = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.IsActive
                        && x.Scope == KnowledgeDocumentScope.Hr
                        && x.OwnerId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var attachedIds = await dbContext.StudioKnowledgeDocuments.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.IsActive && x.KnowledgeDocumentId != null)
            .Select(x => x.KnowledgeDocumentId!.Value)
            .ToListAsync(ct);
        var attachedSet = attachedIds.ToHashSet();

        return kbDocs
            .Select(kb => new StudioLibraryDocumentDto(
                kb.Id,
                kb.FileName,
                kb.Status,
                kb.ChunkCount,
                kb.CreatedAt,
                attachedSet.Contains(kb.Id),
                string.IsNullOrWhiteSpace(kb.Scope) ? KnowledgeDocumentScope.Hr : kb.Scope,
                KnowledgeDocumentType.FromSection(kb.Section)))
            .ToList();
    }

    public async Task<IReadOnlyList<StudioDocumentDto>> AttachFromLibraryAsync(
        Guid projectId, Guid userId, AttachStudioDocumentsRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, requireEdit: true, ct);

        if (request.KnowledgeDocumentIds is null || request.KnowledgeDocumentIds.Count == 0)
            throw new StudioBusinessException("DOCUMENT_IDS_REQUIRED", StatusCodes.Status400BadRequest, "Cần chọn ít nhất 1 tài liệu.");

        var ids = request.KnowledgeDocumentIds.Distinct().ToList();
        var kbDocs = await dbContext.KnowledgeDocuments
            .Where(x => ids.Contains(x.Id) && x.IsActive && x.Scope == KnowledgeDocumentScope.Hr && x.OwnerId == userId)
            .ToListAsync(ct);

        if (kbDocs.Count != ids.Count)
            throw new StudioBusinessException("DOCUMENT_NOT_FOUND", StatusCodes.Status404NotFound,
                "Một số tài liệu không tồn tại hoặc không thuộc Knowledge Base của bạn.");

        foreach (var kb in kbDocs)
        {
            if (!string.Equals(kb.Status, KnowledgeDocumentStatus.Completed, StringComparison.OrdinalIgnoreCase))
                throw new StudioBusinessException("DOCUMENT_NOT_READY", StatusCodes.Status422UnprocessableEntity,
                    $"Tài liệu '{kb.FileName}' chưa RAG Completed — chỉ gắn được khi ingest xong.");
        }

        // Soft-deleted cũ cùng KnowledgeDocumentId → reactivate thay vì tạo trùng
        var existingRows = await dbContext.StudioKnowledgeDocuments
            .Where(x => x.ProjectId == projectId && x.KnowledgeDocumentId != null && ids.Contains(x.KnowledgeDocumentId.Value))
            .ToListAsync(ct);

        var result = new List<StudioDocumentDto>();
        foreach (var kb in kbDocs)
        {
            var existing = existingRows.FirstOrDefault(x => x.KnowledgeDocumentId == kb.Id);
            if (existing is not null && existing.IsActive)
                throw new StudioBusinessException("DOCUMENT_ALREADY_ATTACHED", StatusCodes.Status409Conflict,
                    $"Tài liệu '{kb.FileName}' đã gắn vào project này.");

            StudioKnowledgeDocument row;
            if (existing is not null)
            {
                // Reactivate link đã gỡ trước đó
                existing.IsActive = true;
                existing.FileName = kb.FileName;
                existing.StoragePath = LibraryLinkPrefix + (kb.BlobPath ?? string.Empty);
                existing.FileType = NormalizeFileType(null, kb.FileName);
                existing.ProcessingStatus = MapRagStatus(kb.Status);
                existing.ProcessingError = kb.ErrorMessage;
                existing.IsSelected = request.IsSelected && MapRagStatus(kb.Status) == DocumentProcessingStatus.Completed;
                existing.UpdatedAt = DateTime.UtcNow;
                row = existing;
            }
            else
            {
                row = new StudioKnowledgeDocument
                {
                    ProjectId = projectId,
                    KnowledgeDocumentId = kb.Id,
                    FileName = kb.FileName,
                    FileType = NormalizeFileType(null, kb.FileName),
                    FileSize = 0,
                    StoragePath = LibraryLinkPrefix + (kb.BlobPath ?? string.Empty),
                    IsSelected = request.IsSelected,
                    ProcessingStatus = MapRagStatus(kb.Status),
                    ProcessingError = kb.ErrorMessage
                };
                dbContext.StudioKnowledgeDocuments.Add(row);
            }

            result.Add(Map(row, kb.Status, kb.ChunkCount, kb.ErrorMessage,
                string.IsNullOrWhiteSpace(kb.Scope) ? KnowledgeDocumentScope.Hr : kb.Scope,
                KnowledgeDocumentType.FromSection(kb.Section)));
        }

        await dbContext.SaveChangesAsync(ct);
        return result;
    }

    public async Task<StudioDocumentDto> SetSelectionAsync(Guid projectId, Guid documentId, Guid userId, bool isSelected, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, requireEdit: true, ct);
        var row = await dbContext.StudioKnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == documentId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("DOCUMENT_NOT_FOUND", StatusCodes.Status404NotFound, "Không tìm thấy tài liệu.");

        // Chỉ cho chọn khi RAG đã COMPLETED (giống ý nghĩa Ready)
        if (isSelected && row.KnowledgeDocumentId is Guid kid)
        {
            var kb = await dbContext.KnowledgeDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == kid, ct);
            if (kb is null || !string.Equals(kb.Status, KnowledgeDocumentStatus.Completed, StringComparison.OrdinalIgnoreCase))
                throw new StudioBusinessException("DOCUMENT_NOT_READY", StatusCodes.Status422UnprocessableEntity, "Tài liệu chưa ingest RAG xong.");
        }
        else if (isSelected && row.ProcessingStatus != DocumentProcessingStatus.Completed)
        {
            throw new StudioBusinessException("DOCUMENT_NOT_READY", StatusCodes.Status422UnprocessableEntity, "Tài liệu chưa sẵn sàng.");
        }

        row.IsSelected = isSelected;
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        var scope = KnowledgeDocumentScope.Hr;
        var documentType = KnowledgeDocumentType.Unclassified;
        if (row.KnowledgeDocumentId is Guid selKid)
        {
            var kbRow = await dbContext.KnowledgeDocuments.AsNoTracking()
                .Where(x => x.Id == selKid)
                .Select(x => new { x.Scope, x.Section })
                .FirstOrDefaultAsync(ct);
            if (kbRow is not null)
            {
                if (!string.IsNullOrWhiteSpace(kbRow.Scope))
                    scope = kbRow.Scope;
                documentType = KnowledgeDocumentType.FromSection(kbRow.Section);
            }
        }
        return Map(row, null, null, row.ProcessingError, scope, documentType);
    }

    public async Task DeleteAsync(Guid projectId, Guid documentId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, requireEdit: true, ct);
        var row = await dbContext.StudioKnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == documentId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("DOCUMENT_NOT_FOUND", StatusCodes.Status404NotFound, "Không tìm thấy tài liệu.");

        var isLibraryLink = IsLibraryLink(row.StoragePath);
        // Library attach: chỉ unlink khỏi project — giữ nguyên knowledge_documents / RAG.
        // Upload từ Studio: xóa luôn KB (hành vi cũ).
        if (!isLibraryLink && row.KnowledgeDocumentId is Guid kid)
        {
            try
            {
                await knowledgeDocumentService.DeleteAsync(kid, ownerIdFilter: userId);
            }
            catch
            {
                // Soft-fail: vẫn soft-delete studio row
            }
        }

        row.IsActive = false;
        row.IsSelected = false;
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<StudioDocumentDto> ReingestAsync(Guid projectId, Guid documentId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, requireEdit: true, ct);
        var row = await dbContext.StudioKnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == documentId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("DOCUMENT_NOT_FOUND", StatusCodes.Status404NotFound, "Không tìm thấy tài liệu.");

        if (row.KnowledgeDocumentId is not Guid kid)
            throw new StudioBusinessException("DOCUMENT_NOT_LINKED_TO_RAG", StatusCodes.Status422UnprocessableEntity, "Tài liệu chưa liên kết RAG.");

        var re = await knowledgeDocumentService.ReingestAsync(kid, ownerIdFilter: userId);
        row.ProcessingStatus = MapRagStatus(re.Status);
        row.ProcessingError = re.ErrorMessage;
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        var kbMeta = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.Id == kid)
            .Select(x => new { x.Scope, x.Section })
            .FirstOrDefaultAsync(ct);
        return Map(row, re.Status, re.ChunkCount, re.ErrorMessage,
            string.IsNullOrWhiteSpace(kbMeta?.Scope) ? KnowledgeDocumentScope.Hr : kbMeta!.Scope,
            KnowledgeDocumentType.FromSection(kbMeta?.Section));
    }

    /// <summary>SCRUM-443: gợi ý gắn — retrieve với toàn bộ KB COMPLETED chưa gắn.</summary>
    public async Task<IReadOnlyList<StudioKnowledgeSuggestionDto>> SuggestAttachAsync(
        Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, requireEdit: false, ct);

        var jd = await dbContext.StudioJobDescriptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (jd is null || string.IsNullOrWhiteSpace(jd.Content))
            return [];

        var attachedIds = await dbContext.StudioKnowledgeDocuments.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.IsActive && x.KnowledgeDocumentId != null)
            .Select(x => x.KnowledgeDocumentId!.Value)
            .ToListAsync(ct);
        var attachedSet = attachedIds.ToHashSet();

        var candidates = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.IsActive
                        && x.Scope == KnowledgeDocumentScope.Hr
                        && x.OwnerId == userId
                        && x.Status == KnowledgeDocumentStatus.Completed
                        && !attachedSet.Contains(x.Id))
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return [];

        var candidateIds = candidates.Select(c => c.Id).ToList();
        var retrieve = await ragService.RetrieveAsync(new ApplicationLayer.DTOs.Rag.RagRetrieveRequest
        {
            OwnerId = userId,
            JobDescription = jd.Content,
            DocumentIds = candidateIds,
            TopKHr = Math.Min(30, Math.Max(10, candidateIds.Count * 2))
        }, ct);

        if (!retrieve.Success || retrieve.HrChunks.Count == 0)
            return [];

        var byDoc = retrieve.HrChunks
            .GroupBy(c => c.DocumentId)
            .Select(g =>
            {
                var kb = candidates.FirstOrDefault(c => c.Id == g.Key);
                var best = g.OrderByDescending(x => x.Score).First();
                return new StudioKnowledgeSuggestionDto(
                    g.Key,
                    kb?.FileName ?? best.FileName ?? g.Key.ToString(),
                    KnowledgeDocumentType.FromSection(kb?.Section ?? best.Section),
                    g.Max(x => x.Score),
                    g.Count(),
                    best.Content);
            })
            .OrderByDescending(s => s.MaxScore)
            .Take(8)
            .ToList();

        return byDoc;
    }

    /// <summary>SCRUM-444: preview retrieve 1 tài liệu với JD hiện tại.</summary>
    public async Task<StudioRetrievePreviewDto> RetrievePreviewAsync(
        Guid projectId, Guid userId, StudioRetrievePreviewRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, requireEdit: false, ct);

        var kb = await dbContext.KnowledgeDocuments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.KnowledgeDocumentId
                                      && x.IsActive
                                      && x.Scope == KnowledgeDocumentScope.Hr
                                      && x.OwnerId == userId, ct)
            ?? throw new StudioBusinessException("DOCUMENT_NOT_FOUND", StatusCodes.Status404NotFound,
                "Tài liệu không tồn tại trong Knowledge Base của bạn.");

        var jd = await dbContext.StudioJobDescriptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("JD_REQUIRED", StatusCodes.Status422UnprocessableEntity,
                "Cần Job Description để xem preview retrieve.");

        if (string.IsNullOrWhiteSpace(jd.Content))
            throw new StudioBusinessException("JD_EMPTY", StatusCodes.Status422UnprocessableEntity, "Job Description đang trống.");

        var retrieve = await ragService.RetrieveAsync(new ApplicationLayer.DTOs.Rag.RagRetrieveRequest
        {
            OwnerId = userId,
            JobDescription = jd.Content,
            DocumentIds = [kb.Id],
            TopKHr = 5
        }, ct);

        if (!retrieve.Success)
            throw new StudioBusinessException("RETRIEVE_FAILED", StatusCodes.Status502BadGateway,
                retrieve.Error ?? "Retrieve thất bại.");

        var chunks = retrieve.HrChunks
            .Select(c => new StudioRetrievePreviewChunkDto(c.ChunkIndex, c.Content, c.Score, c.FileName ?? kb.FileName))
            .ToList();

        return new StudioRetrievePreviewDto(kb.Id, chunks);
    }

    private static bool IsLibraryLink(string? storagePath)
        => !string.IsNullOrEmpty(storagePath)
           && storagePath.StartsWith(LibraryLinkPrefix, StringComparison.OrdinalIgnoreCase);

    private static DocumentProcessingStatus MapRagStatus(string? ragStatus)
    {
        if (string.IsNullOrWhiteSpace(ragStatus))
            return DocumentProcessingStatus.Pending;

        return ragStatus.ToUpperInvariant() switch
        {
            "QUEUED" => DocumentProcessingStatus.Pending,
            "PROCESSING" => DocumentProcessingStatus.Processing,
            "COMPLETED" => DocumentProcessingStatus.Completed,
            "FAILED" => DocumentProcessingStatus.Failed,
            _ => DocumentProcessingStatus.Pending
        };
    }

    private static StudioDocumentDto Map(
        StudioKnowledgeDocument x,
        string? ragStatus,
        int? chunkCount,
        string? error,
        string scope = KnowledgeDocumentScope.Hr,
        string documentType = KnowledgeDocumentType.Unclassified)
        => new(
            x.Id,
            x.FileName,
            x.FileType,
            x.FileSize,
            x.IsSelected,
            x.ProcessingStatus,
            x.ExtractedText is null ? null : x.ExtractedText[..Math.Min(300, x.ExtractedText.Length)],
            x.KnowledgeDocumentId,
            ragStatus,
            chunkCount,
            error,
            IsLibraryLink(x.StoragePath),
            string.IsNullOrWhiteSpace(scope) ? KnowledgeDocumentScope.Hr : scope,
            string.IsNullOrWhiteSpace(documentType) ? KnowledgeDocumentType.Unclassified : documentType);
}
