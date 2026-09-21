using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class KnowledgeDocumentRepository : IKnowledgeDocumentRepository
{
    private readonly Database.AppDbContext _context;

    public KnowledgeDocumentRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<KnowledgeDocument?> GetByIdAsync(Guid id)
        => _context.KnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == id);

    public async Task AddAsync(KnowledgeDocument document)
    {
        // DB RAG: updated_at NOT NULL — EF không tự điền khi insert
        var now = DateTime.UtcNow;
        if (document.CreatedAt == default)
            document.CreatedAt = now;
        document.UpdatedAt ??= now;

        await _context.KnowledgeDocuments.AddAsync(document);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(KnowledgeDocument document)
    {
        document.UpdatedAt = DateTime.UtcNow;
        _context.KnowledgeDocuments.Update(document);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteHardAsync(KnowledgeDocument document)
    {
        _context.KnowledgeDocuments.Remove(document);
        await _context.SaveChangesAsync();
    }

    public async Task<PagedResultDto<KnowledgeDocument>> GetPagedAsync(KnowledgeDocumentQueryDto query)
    {
        var q = _context.KnowledgeDocuments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Scope))
            q = q.Where(d => d.Scope == query.Scope);

        if (query.OwnerId.HasValue)
            q = q.Where(d => d.OwnerId == query.OwnerId);

        if (!string.IsNullOrWhiteSpace(query.FileName))
        {
            var name = query.FileName.Trim().ToLower();
            q = q.Where(d => d.FileName.ToLower().Contains(name));
        }

        if (query.FromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(query.FromDate.Value.Date, DateTimeKind.Utc);
            q = q.Where(d => d.CreatedAt >= from);
        }

        if (query.ToDate.HasValue)
        {
            var toExclusive = DateTime.SpecifyKind(query.ToDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            q = q.Where(d => d.CreatedAt < toExclusive);
        }

        // SCRUM-450: lọc theo folder UI
        if (!string.IsNullOrWhiteSpace(query.Folder))
        {
            var folderKey = query.Folder.Trim().ToLowerInvariant();
            if (folderKey == KnowledgeFolderHelper.UnsortedKey)
                q = q.Where(d => d.Folder == null || d.Folder == "");
            else
                q = q.Where(d => d.Folder == folderKey);
        }

        var total = await q.CountAsync();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await q
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<KnowledgeDocument>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<KnowledgeDocument>> GetStuckProcessingAsync(DateTime updatedBeforeUtc)
        => await _context.KnowledgeDocuments
            .Where(d => d.Status == DomainLayer.Constants.KnowledgeDocumentStatus.Processing
                && d.UpdatedAt != null
                && d.UpdatedAt < updatedBeforeUtc)
            .ToListAsync();

    public Task<KnowledgeDocument?> FindByContentHashAsync(string contentHash, string scope, Guid? ownerId)
    {
        var q = _context.KnowledgeDocuments.AsQueryable()
            .Where(d => d.IsActive
                        && d.ContentHash == contentHash
                        && d.Scope == scope);

        if (ownerId.HasValue)
            q = q.Where(d => d.OwnerId == ownerId);
        else
            q = q.Where(d => d.OwnerId == null);

        return q.OrderByDescending(d => d.CreatedAt).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<(Guid Id, int ChunkIndex, string Content)>> GetChunksPreviewAsync(Guid documentId, int take)
    {
        take = Math.Clamp(take, 1, 50);
        var rows = await _context.KnowledgeChunks.AsNoTracking()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .Take(take)
            .Select(c => new { c.Id, c.ChunkIndex, c.Content })
            .ToListAsync();

        return rows.Select(c => (c.Id, c.ChunkIndex, c.Content ?? string.Empty)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountStudioProjectsByDocumentIdsAsync(IReadOnlyList<Guid> documentIds)
    {
        if (documentIds.Count == 0)
            return new Dictionary<Guid, int>();

        // Load rồi group in-memory — tránh EF không dịch Distinct().Count() trong GroupBy (Npgsql).
        var rows = await _context.StudioKnowledgeDocuments.AsNoTracking()
            .Where(x => x.IsActive && x.KnowledgeDocumentId != null && documentIds.Contains(x.KnowledgeDocumentId.Value))
            .Select(x => new { DocumentId = x.KnowledgeDocumentId!.Value, x.ProjectId })
            .ToListAsync();

        return rows
            .GroupBy(x => x.DocumentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ProjectId).Distinct().Count());
    }

    public async Task<IReadOnlyDictionary<string, int>> CountCitationsByFileNamesAsync(Guid ownerId, IReadOnlyList<string> fileNames)
    {
        // Ước lượng: load TagsJson câu hỏi thuộc project của owner, đếm sourceFile khớp tên.
        var names = fileNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var projectIds = await _context.InterviewProjects.AsNoTracking()
            .Where(p => p.OwnerId == ownerId && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync();

        if (projectIds.Count == 0)
            return names.ToDictionary(n => n, _ => 0, StringComparer.OrdinalIgnoreCase);

        var tagsList = await _context.InterviewQuestions.AsNoTracking()
            .Where(q => q.IsActive && projectIds.Contains(q.ProjectId) && q.TagsJson != null && q.TagsJson != "" && q.TagsJson != "[]")
            .Select(q => q.TagsJson!)
            .Take(2000)
            .ToListAsync();

        var counts = names.ToDictionary(n => n, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var tags in tagsList)
        {
            foreach (var name in names)
            {
                // Khớp nhanh sourceFile / source_file trong JSON citations
                if (tags.Contains(name, StringComparison.OrdinalIgnoreCase))
                    counts[name]++;
            }
        }

        return counts;
    }

    public async Task<IReadOnlyList<Guid>> ListSystemDocumentIdsByTypeAsync(string documentType)
    {
        var type = KnowledgeDocumentType.NormalizeForStorage(documentType, requireHrType: false);
        return await _context.KnowledgeDocuments.AsNoTracking()
            .Where(d => d.IsActive
                        && d.Scope == KnowledgeDocumentScope.System
                        && d.Status == KnowledgeDocumentStatus.Completed
                        && d.Section == type)
            .Select(d => d.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Guid>> ListSystemDocumentIdsByFolderAsync(string folder)
    {
        // Folder null/unsorted → không trả doc (Coach bắt buộc folder có tên, vd. test-candidate).
        var folderKey = KnowledgeFolderHelper.Normalize(folder);
        if (folderKey is null)
            return Array.Empty<Guid>();

        return await _context.KnowledgeDocuments.AsNoTracking()
            .Where(d => d.IsActive
                        && d.Scope == KnowledgeDocumentScope.System
                        && d.Status == KnowledgeDocumentStatus.Completed
                        && d.Folder == folderKey)
            .Select(d => d.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<KnowledgeFolderDto>> ListFoldersAsync(string scope)
    {
        var rows = await _context.KnowledgeDocuments.AsNoTracking()
            .Where(d => d.Scope == scope)
            .GroupBy(d => d.Folder == null || d.Folder == "" ? null : d.Folder)
            .Select(g => new { Folder = g.Key, Count = g.Count() })
            .ToListAsync();

        return rows
            .Select(r => new KnowledgeFolderDto
            {
                Name = KnowledgeFolderHelper.DisplayName(r.Folder),
                Count = r.Count
            })
            .OrderBy(x => x.Name == KnowledgeFolderHelper.UnsortedKey ? 1 : 0)
            .ThenBy(x => x.Name)
            .ToList();
    }

    public async Task<int> RenameFolderAsync(string scope, string? fromFolder, string? toFolder)
    {
        var q = _context.KnowledgeDocuments.Where(d => d.Scope == scope);
        if (fromFolder is null)
            q = q.Where(d => d.Folder == null || d.Folder == "");
        else
            q = q.Where(d => d.Folder == fromFolder);

        var docs = await q.ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var d in docs)
        {
            d.Folder = toFolder;
            d.UpdatedAt = now;
        }

        if (docs.Count > 0)
            await _context.SaveChangesAsync();
        return docs.Count;
    }

    public async Task<int> MoveDocumentsAsync(string scope, IReadOnlyList<Guid> documentIds, string? toFolder)
    {
        if (documentIds.Count == 0)
            return 0;

        var docs = await _context.KnowledgeDocuments
            .Where(d => d.Scope == scope && documentIds.Contains(d.Id))
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var d in docs)
        {
            d.Folder = toFolder;
            d.UpdatedAt = now;
        }

        if (docs.Count > 0)
            await _context.SaveChangesAsync();
        return docs.Count;
    }
}
