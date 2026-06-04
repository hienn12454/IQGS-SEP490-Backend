using ApplicationLayer.DTOs.Admin;
using DomainLayer.Entities;
using InfrastructureLayer.Database;
using ApplicationLayer.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email)
        => await _dbSet.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

    public async Task<User?> GetByGoogleIdAsync(string googleId)
        => await _dbSet.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.GoogleId == googleId && u.IsActive);

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
        => await _dbSet.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.IsActive);

    public async Task<User?> GetByPasswordResetTokenAsync(string tokenHash)
        => await _dbSet.FirstOrDefaultAsync(u => u.PasswordResetToken == tokenHash && u.IsActive);

    public async Task<User?> GetByEmailVerificationTokenAsync(string tokenHash)
        => await _dbSet.FirstOrDefaultAsync(u => u.EmailVerificationToken == tokenHash);

    public async Task<User?> GetByIdAnyStatusAsync(Guid id)
        => await _dbSet.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);

    public async Task LoadRoleAsync(User user)
    {
        if (user.Role == null!)
            await _context.Entry(user).Reference(u => u.Role).LoadAsync();
    }

    public async Task<(List<User> Users, int Total)> GetPagedAsync(UserQueryDto query)
    {
        var q = _dbSet.Include(u => u.Role).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            q = q.Where(u => EF.Functions.ILike(u.FullName, term)
                           || EF.Functions.ILike(u.Email, term));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
            q = q.Where(u => u.Role.Name == query.Role);

        if (query.IsActive.HasValue)
            q = q.Where(u => u.IsActive == query.IsActive.Value);

        var total = await q.CountAsync();
        var users = await q
            .OrderByDescending(u => u.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return (users, total);
    }
}
