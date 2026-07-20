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

    public async Task AddRangeAsync(IReadOnlyList<Company> companies)
    {
        await _dbSet.AddRangeAsync(companies);
        await _context.SaveChangesAsync();
    }
}
