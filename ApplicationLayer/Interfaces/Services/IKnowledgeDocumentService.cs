using ApplicationLayer.DTOs.KnowledgeBase;

namespace ApplicationLayer.Interfaces.Services;

public interface IKnowledgeDocumentService
{
    Task<KnowledgeDocumentResponseDto> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSize,
        KnowledgeDocumentUploadDto dto,
        Guid uploadedBy,
        CancellationToken ct = default);

    Task<PagedResultDto<KnowledgeDocumentResponseDto>> GetPagedAsync(
        KnowledgeDocumentListQueryDto query,
        string? scope = null,
        Guid? ownerId = null);
    Task<KnowledgeDocumentResponseDto> GetByIdAsync(Guid id, Guid? ownerIdFilter = null);
    Task<KnowledgeDocumentResponseDto> ReingestAsync(Guid id, Guid? ownerIdFilter = null);
    Task DeleteAsync(Guid id, Guid? ownerIdFilter = null);

    /// <summary>SCRUM-442: đổi loại tài liệu (section).</summary>
    Task<KnowledgeDocumentResponseDto> UpdateDocumentTypeAsync(
        Guid id,
        string documentType,
        Guid? ownerIdFilter = null,
        bool requireHrType = true);

    /// <summary>SCRUM-447/450: Admin cập nhật type, AdminNote và/hoặc Folder.</summary>
    Task<KnowledgeDocumentResponseDto> UpdateDocumentMetaAsync(
        Guid id,
        string? documentType,
        string? adminNote,
        string? folder = null,
        bool updateFolder = false,
        Guid? ownerIdFilter = null);

    /// <summary>SCRUM-444: vài chunk đầu để preview.</summary>
    Task<IReadOnlyList<KnowledgeChunkPreviewDto>> GetChunksAsync(
        Guid id,
        Guid? ownerIdFilter = null,
        int take = 20);

    /// <summary>SCRUM-450: danh sách folder + số file (SYSTEM).</summary>
    Task<IReadOnlyList<KnowledgeFolderDto>> ListFoldersAsync(string scope);

    /// <summary>SCRUM-451: đổi tên folder (metadata bulk).</summary>
    Task<FolderMutationResultDto> RenameFolderAsync(string scope, string from, string? to);

    /// <summary>SCRUM-451: chuyển nhiều document sang folder.</summary>
    Task<FolderMutationResultDto> MoveDocumentsAsync(string scope, IReadOnlyList<Guid> documentIds, string? folder);
}
