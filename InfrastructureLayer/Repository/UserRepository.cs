using ApplicationLayer.DTOs.Admin;
using DomainLayer.Entities;
using InfrastructureLayer.Database;
using ApplicationLayer.Interfaces.Repositories;
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

    public async Task<User?> GetByGithubIdAsync(string githubId)
        => await _dbSet.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.GithubId == githubId && u.IsActive);

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
        => await _dbSet.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.IsActive);

    public async Task<User?> GetByPasswordResetTokenAsync(string tokenHash)
        => await _dbSet.FirstOrDefaultAsync(u => u.PasswordResetToken == tokenHash && u.IsActive);

    public async Task<User?> GetByEmailVerificationTokenAsync(string tokenHash)
        => await _dbSet.FirstOrDefaultAsync(u => u.EmailVerificationToken == tokenHash);

    public async Task<User?> GetByIdAnyStatusAsync(Guid id)
        => await _dbSet.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);

    public async Task<User?> GetByEmailAnyStatusAsync(string email)
        => await _dbSet.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);

    public async Task<User?> GetByGoogleIdAnyStatusAsync(string googleId)
        => await _dbSet.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.GoogleId == googleId);

    public async Task<User?> GetByGithubIdAnyStatusAsync(string githubId)
        => await _dbSet.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.GithubId == githubId);

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

        if (query.IsEmailVerified.HasValue)
            q = q.Where(u => u.IsEmailVerified == query.IsEmailVerified.Value);

        if (query.CreatedFrom.HasValue)
        {
            var from = query.CreatedFrom.Value.Date;
            q = q.Where(u => u.CreatedAt >= from);
        }

        if (query.CreatedTo.HasValue)
        {
            // Inclusive cả ngày CreatedTo
            var toExclusive = query.CreatedTo.Value.Date.AddDays(1);
            q = q.Where(u => u.CreatedAt < toExclusive);
        }

        // Lọc gói Free / Premium theo subscription còn hạn
        if (!string.IsNullOrWhiteSpace(query.Plan))
        {
            var planKey = query.Plan.Trim();
            var now = DateTime.UtcNow;

            var premiumUserIds = _context.Subscriptions.AsNoTracking()
                .Where(s => s.IsActive && s.CurrentPeriodEnd > now)
                .Join(
                    _context.SubscriptionPlans.AsNoTracking(),
                    s => s.PlanId,
                    p => p.Id,
                    (s, p) => new { s.UserId, p.Code })
                .Where(x => x.Code.Contains("PREMIUM"))
                .Select(x => x.UserId);

            if (string.Equals(planKey, "Premium", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(u => premiumUserIds.Contains(u.Id));
            }
            else if (string.Equals(planKey, "Free", StringComparison.OrdinalIgnoreCase))
            {
                // Free: không thuộc nhóm Premium active (gồm user chưa có sub)
                q = q.Where(u => !premiumUserIds.Contains(u.Id));
            }
        }

        var total = await q.CountAsync();
        var users = await q
            .OrderByDescending(u => u.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return (users, total);
    }
}
