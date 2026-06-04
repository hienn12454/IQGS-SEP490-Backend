using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces;

public interface IHRProfileRepository : IBaseRepository<HRProfile>
{
    Task<HRProfile?> GetByUserIdAsync(Guid userId);
}
