using System.Text;
using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services.Coach;

public interface ICoachKnowledgeViewService
{
    Task<CoachKnowledgeViewDto> GetViewForCandidateAsync(Guid candidateUserId, Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, bool>> GetAllowCandidateViewMapAsync(IReadOnlyCollection<Guid> documentIds);
}

/// <summary>SCRUM-486: Candidate xem file KB gắn roadmap — ACL AllowCandidateView + ownership roadmap.</summary>
public class CoachKnowledgeViewService : ICoachKnowledgeViewService
{
    private static readonly TimeSpan SasExpiry = TimeSpan.FromMinutes(30);

    private readonly IKnowledgeDocumentRepository _docs;
    private readonly ICandidateRoadmapRepository _roadmaps;
    private readonly IBlobStorageService _blob;

    public CoachKnowledgeViewService(
        IKnowledgeDocumentRepository docs,
        ICandidateRoadmapRepository roadmaps,
        IBlobStorageService blob)
    {
        _docs = docs;
        _roadmaps = roadmaps;
        _blob = blob;
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> GetAllowCandidateViewMapAsync(IReadOnlyCollection<Guid> documentIds)
    {
        if (documentIds.Count == 0)
            return new Dictionary<Guid, bool>();
        return await _docs.GetAllowCandidateViewMapAsync(documentIds);
    }

    public async Task<CoachKnowledgeViewDto> GetViewForCandidateAsync(
        Guid candidateUserId, Guid documentId, CancellationToken ct = default)
    {
        var doc = await _docs.GetByIdAsync(documentId)
            ?? throw new NotFoundException("Tài liệu không tồn tại.");

        if (!doc.IsActive
            || !string.Equals(doc.Scope, KnowledgeDocumentScope.System, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Không được xem tài liệu này.");

        if (!doc.AllowCandidateView)
            throw new ForbiddenException("Admin chưa cho phép xem tài liệu này.");

        // Doc phải gắn RoadmapNode của roadmap thuộc candidate (tránh đoán UUID mọi file allow).
        var roadmaps = await _roadmaps.ListByCandidateAsync(candidateUserId);
        var linked = roadmaps
            .SelectMany(r => r.Items)
            .Any(i => i.RoadmapNode?.KnowledgeDocumentId == documentId);
        if (!linked)
            throw new ForbiddenException("Tài liệu không thuộc lộ trình của bạn.");

        var ext = Path.GetExtension(doc.FileName)?.ToLowerInvariant() ?? "";
        var dto = new CoachKnowledgeViewDto
        {
            DocumentId = doc.Id,
            FileName = doc.FileName,
            SourceTitle = doc.SourceTitle
        };

        switch (ext)
        {
            case ".md":
                dto.ContentType = "markdown";
                dto.Content = Encoding.UTF8.GetString(await _blob.DownloadAsync(doc.BlobPath, ct));
                break;
            case ".txt":
                dto.ContentType = "text";
                dto.Content = Encoding.UTF8.GetString(await _blob.DownloadAsync(doc.BlobPath, ct));
                break;
            case ".pdf":
                dto.ContentType = "pdf";
                dto.ExpiresAt = DateTime.UtcNow.Add(SasExpiry);
                dto.Url = await _blob.GenerateReadSasUrlAsync(doc.BlobPath, SasExpiry, ct);
                break;
            case ".docx":
                dto.ContentType = "docx";
                dto.ExpiresAt = DateTime.UtcNow.Add(SasExpiry);
                dto.Url = await _blob.GenerateReadSasUrlAsync(doc.BlobPath, SasExpiry, ct);
                var chunks = await _docs.GetChunksPreviewAsync(doc.Id, take: 30);
                if (chunks.Count > 0)
                    dto.PreviewText = string.Join("\n\n", chunks.OrderBy(c => c.ChunkIndex).Select(c => c.Content));
                break;
            default:
                throw new BadRequestException($"Định dạng '{ext}' chưa hỗ trợ xem trong Coach.");
        }

        return dto;
    }
}
