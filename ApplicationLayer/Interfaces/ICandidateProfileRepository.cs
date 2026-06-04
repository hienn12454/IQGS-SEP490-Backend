using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces;

public interface ICandidateProfileRepository : IBaseRepository<CandidateProfile>
{
    Task<CandidateProfile?> GetByUserIdAsync(Guid userId);
}
