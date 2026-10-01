using DomainLayer.Entities;
using InfrastructureLayer.Database;
using ApplicationLayer.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class CompanyRepository : BaseRepository<Company>, ICompanyRepository
{
    public CompanyRepository(AppDbContext context) : base(context) { }

    public async Task<Company?> GetByNameAsync(string name)
        => await _dbSet.FirstOrDefaultAsync(c => c.Name == name && c.IsActive);

    public async Task<List<Company>> SearchAsync(string? keyword, int limit = 50)
    {
        var q = _dbSet.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = $"%{keyword.Trim()}%";
            q = q.Where(c => EF.Functions.ILike(c.Name, term));
        }

        return await q.OrderBy(c => c.Name).Take(limit).ToListAsync();
    }

    public async Task<(List<Company> Items, int TotalCount)> SearchPagedAsync(string? keyword, int page, int pageSize)
    {
        // Chuẩn hoá phân trang — tránh Skip âm / pageSize = 0
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _dbSet.AsNoTracking().Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = $"%{keyword.Trim()}%";
            q = q.Where(c => EF.Functions.ILike(c.Name, term));
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task AddRangeAsync(IReadOnlyList<Company> companies)
    {
        await _dbSet.AddRangeAsync(companies);
        await _context.SaveChangesAsync();
    }
}
