using DomainLayer.Entities;
using InfrastructureLayer.Database;
using ApplicationLayer.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class CandidateProfileRepository : BaseRepository<CandidateProfile>, ICandidateProfileRepository
{
    public CandidateProfileRepository(AppDbContext context) : base(context) { }

    public async Task<CandidateProfile?> GetByUserIdAsync(Guid userId)
        => await _dbSet.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);
}
