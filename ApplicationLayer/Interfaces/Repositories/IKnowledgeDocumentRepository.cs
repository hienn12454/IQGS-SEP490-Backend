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
}
