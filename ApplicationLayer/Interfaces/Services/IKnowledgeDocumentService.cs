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

    /// <summary>SCRUM-444: vài chunk đầu để preview.</summary>
    Task<IReadOnlyList<KnowledgeChunkPreviewDto>> GetChunksAsync(
        Guid id,
        Guid? ownerIdFilter = null,
        int take = 20);
}
