using DomainLayer.Entities;
using InfrastructureLayer.Database;
using InfrastructureLayer.Repository.Interface;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email)
        => await _dbSet.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
}
