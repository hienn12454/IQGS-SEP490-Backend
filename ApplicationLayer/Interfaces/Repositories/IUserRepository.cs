using ApplicationLayer.DTOs.Admin;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IUserRepository : IBaseRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByGoogleIdAsync(string googleId);
    Task<User?> GetByRefreshTokenAsync(string refreshToken);
    Task<User?> GetByPasswordResetTokenAsync(string tokenHash);
    Task<User?> GetByEmailVerificationTokenAsync(string tokenHash);
    Task<(List<User> Users, int Total)> GetPagedAsync(UserQueryDto query);

    /// <summary>Tìm user bất kể IsActive — dùng cho Admin.</summary>
    Task<User?> GetByIdAnyStatusAsync(Guid id);

    /// <summary>Load Role navigation property.</summary>
    Task LoadRoleAsync(User user);
}
