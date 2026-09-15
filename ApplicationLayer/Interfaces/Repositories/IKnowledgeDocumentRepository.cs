using ApplicationLayer.DTOs.KnowledgeBase;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IKnowledgeDocumentRepository
{
    Task<KnowledgeDocument?> GetByIdAsync(Guid id);
    Task AddAsync(KnowledgeDocument document);
    Task UpdateAsync(KnowledgeDocument document);
    Task DeleteHardAsync(KnowledgeDocument document);
    Task<PagedResultDto<KnowledgeDocument>> GetPagedAsync(KnowledgeDocumentQueryDto query);
    Task<IReadOnlyList<KnowledgeDocument>> GetStuckProcessingAsync(DateTime updatedBeforeUtc);

    /// <summary>SCRUM-442: tìm doc trùng hash cùng scope + owner (chống upload trùng).</summary>
    Task<KnowledgeDocument?> FindByContentHashAsync(string contentHash, string scope, Guid? ownerId);

    /// <summary>SCRUM-444: lấy N chunk đầu theo document.</summary>
    Task<IReadOnlyList<(Guid Id, int ChunkIndex, string Content)>> GetChunksPreviewAsync(Guid documentId, int take);

    /// <summary>SCRUM-444: số project Studio đang gắn từng KnowledgeDocumentId.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountStudioProjectsByDocumentIdsAsync(IReadOnlyList<Guid> documentIds);

    /// <summary>
    /// SCRUM-444: đếm citation theo FileName trong TagsJson (cùng owner).
    /// Ước lượng — trùng tên file có thể đếm nhầm.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> CountCitationsByFileNamesAsync(Guid ownerId, IReadOnlyList<string> fileNames);

    /// <summary>SCRUM-447: danh sách documentId SYSTEM theo document type (section).</summary>
    Task<IReadOnlyList<Guid>> ListSystemDocumentIdsByTypeAsync(string documentType);

    /// <summary>SCRUM-450: đếm document theo folder trong một scope.</summary>
    Task<IReadOnlyList<KnowledgeFolderDto>> ListFoldersAsync(string scope);

    /// <summary>SCRUM-451: đổi Folder cho mọi doc cùng scope + fromFolder.</summary>
    Task<int> RenameFolderAsync(string scope, string? fromFolder, string? toFolder);

    /// <summary>SCRUM-451: gán Folder cho danh sách documentIds (cùng scope).</summary>
    Task<int> MoveDocumentsAsync(string scope, IReadOnlyList<Guid> documentIds, string? toFolder);
}
