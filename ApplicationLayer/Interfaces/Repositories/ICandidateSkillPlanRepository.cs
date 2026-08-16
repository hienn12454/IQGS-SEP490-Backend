using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface ICandidateSkillPlanRepository
{
    Task<CandidateSkillPlan?> GetByCandidateUserIdAsync(Guid candidateUserId);
    Task AddAsync(CandidateSkillPlan plan);
    Task UpdateAsync(CandidateSkillPlan plan);
}
