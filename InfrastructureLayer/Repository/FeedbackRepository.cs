using ApplicationLayer.DTOs.Feedback;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using InfrastructureLayer.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class FeedbackRepository : BaseRepository<Feedback>, IFeedbackRepository
{
    public FeedbackRepository(AppDbContext context) : base(context) { }

    public async Task<List<Feedback>> GetApprovedAsync(int limit)
        => await _dbSet
            .Include(f => f.User)
            .Where(f => f.IsActive && f.Status == FeedbackStatus.Approved)
            .OrderByDescending(f => f.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<(List<Feedback> Items, int TotalCount)> GetPagedAsync(FeedbackQueryDto query)
    {
        var q = _dbSet
            .Include(f => f.User)
            .ThenInclude(u => u.Role)
            .Where(f => f.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(f => f.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Role))
            q = q.Where(f => f.User.Role.Name == query.Role);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            q = q.Where(f => EF.Functions.ILike(f.Content, term) || EF.Functions.ILike(f.User.FullName, term));
        }

        var totalCount = await q.CountAsync();

        var items = await q
            .OrderByDescending(f => f.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
