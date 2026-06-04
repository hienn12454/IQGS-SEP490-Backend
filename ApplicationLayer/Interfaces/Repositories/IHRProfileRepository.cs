using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IHRProfileRepository : IBaseRepository<HRProfile>
{
    Task<HRProfile?> GetByUserIdAsync(Guid userId);
}
