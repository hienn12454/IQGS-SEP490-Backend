using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class CandidateSkillPlanRepository : ICandidateSkillPlanRepository
{
    private readonly Database.AppDbContext _db;

    public CandidateSkillPlanRepository(Database.AppDbContext db)
    {
        _db = db;
    }

    public Task<CandidateSkillPlan?> GetByCandidateUserIdAsync(Guid candidateUserId)
        => _db.CandidateSkillPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.CandidateUserId == candidateUserId && p.IsActive);

    public async Task AddAsync(CandidateSkillPlan plan)
    {
        await _db.CandidateSkillPlans.AddAsync(plan);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(CandidateSkillPlan plan)
    {
        plan.UpdatedAt = DateTime.UtcNow;
        _db.CandidateSkillPlans.Update(plan);
        await _db.SaveChangesAsync();
    }
}
