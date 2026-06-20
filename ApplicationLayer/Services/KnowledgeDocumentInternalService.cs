using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class KnowledgeDocumentInternalService : IKnowledgeDocumentInternalService
{
    private readonly IKnowledgeDocumentRepository _repository;

    public KnowledgeDocumentInternalService(IKnowledgeDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<KnowledgeDocumentResponseDto> UpdateStatusAsync(Guid id, UpdateKnowledgeDocumentStatusDto dto)
    {
        var allowed = new[] { KnowledgeDocumentStatus.Processing, KnowledgeDocumentStatus.Completed, KnowledgeDocumentStatus.Failed };
        if (!allowed.Contains(dto.Status))
            throw new BadRequestException("Status chỉ được PROCESSING, COMPLETED hoặc FAILED.");

        var document = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException("Knowledge document không tồn tại.");

        document.Status = dto.Status;
        if (dto.ChunkCount.HasValue)
            document.ChunkCount = dto.ChunkCount;
        if (dto.ErrorMessage is not null)
            document.ErrorMessage = dto.ErrorMessage;

        await _repository.UpdateAsync(document);
        return MapToDto(document);
    }

    internal static KnowledgeDocumentResponseDto MapToDto(DomainLayer.Entities.KnowledgeDocument document)
    {
        return new KnowledgeDocumentResponseDto
        {
            DocumentId = document.Id,
            Scope = document.Scope,
            OwnerId = document.OwnerId,
            FileName = document.FileName,
            Status = document.Status,
            ChunkCount = document.ChunkCount,
            UploadedBy = document.UploadedBy,
            ErrorMessage = document.ErrorMessage,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };
    }
}
