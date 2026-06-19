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
}
