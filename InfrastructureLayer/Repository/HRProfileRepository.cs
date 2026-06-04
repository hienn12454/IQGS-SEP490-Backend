using DomainLayer.Entities;
using InfrastructureLayer.Database;
using ApplicationLayer.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class HRProfileRepository : BaseRepository<HRProfile>, IHRProfileRepository
{
    public HRProfileRepository(AppDbContext context) : base(context) { }

    public async Task<HRProfile?> GetByUserIdAsync(Guid userId)
        => await _dbSet.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);
}
