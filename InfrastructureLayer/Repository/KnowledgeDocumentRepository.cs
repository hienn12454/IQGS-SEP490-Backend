using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.Interfaces.Repositories;
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
}
