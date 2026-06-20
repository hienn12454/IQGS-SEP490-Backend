using ApplicationLayer.DTOs.KnowledgeBase;

namespace ApplicationLayer.Interfaces.Services;

public interface IKnowledgeDocumentInternalService
{
    Task<KnowledgeDocumentResponseDto> UpdateStatusAsync(Guid id, UpdateKnowledgeDocumentStatusDto dto);
}
