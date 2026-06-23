using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services;

public class KnowledgeDocumentInternalService : IKnowledgeDocumentInternalService
{
    private readonly IKnowledgeDocumentRepository _repository;
    private readonly ILogger<KnowledgeDocumentInternalService> _logger;

    public KnowledgeDocumentInternalService(
        IKnowledgeDocumentRepository repository,
        ILogger<KnowledgeDocumentInternalService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<KnowledgeDocumentResponseDto> UpdateStatusAsync(Guid id, UpdateKnowledgeDocumentStatusDto dto)
    {
        var allowed = new[] { KnowledgeDocumentStatus.Processing, KnowledgeDocumentStatus.Completed, KnowledgeDocumentStatus.Failed };
        if (!allowed.Contains(dto.Status))
            throw new BadRequestException("Status chỉ được PROCESSING, COMPLETED hoặc FAILED.");

        var document = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException("Knowledge document không tồn tại.");

        if (!IsTransitionAllowed(document.Status, dto.Status))
        {
            _logger.LogWarning(
                "Bỏ qua callback status {Next} cho document {DocumentId} — hiện tại {Current}",
                dto.Status, id, document.Status);
            return MapToDto(document);
        }

        document.Status = dto.Status;
        if (dto.ChunkCount.HasValue)
            document.ChunkCount = dto.ChunkCount;
        if (dto.ErrorMessage is not null)
            document.ErrorMessage = dto.ErrorMessage;

        await _repository.UpdateAsync(document);
        return MapToDto(document);
    }

    /// <summary>
    /// Chặn callback cũ ghi đè trạng thái terminal không hợp lệ.
    /// </summary>
    public static bool IsTransitionAllowed(string current, string next)
    {
        if (string.Equals(current, next, StringComparison.Ordinal))
            return true;

        if (current == KnowledgeDocumentStatus.Completed
            && next is KnowledgeDocumentStatus.Failed or KnowledgeDocumentStatus.Processing)
            return false;

        return true;
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
