using DomainLayer.Entities;

namespace InfrastructureLayer.Repository.Interface;

public interface IUserRepository : IBaseRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
}
