using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface ICandidateProfileRepository : IBaseRepository<CandidateProfile>
{
    Task<CandidateProfile?> GetByUserIdAsync(Guid userId);
}
