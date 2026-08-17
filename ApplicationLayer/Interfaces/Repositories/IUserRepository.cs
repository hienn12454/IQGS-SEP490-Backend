using ApplicationLayer.DTOs.Admin;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IUserRepository : IBaseRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByGoogleIdAsync(string googleId);
    Task<User?> GetByGithubIdAsync(string githubId);
    Task<User?> GetByRefreshTokenAsync(string refreshToken);
    Task<User?> GetByPasswordResetTokenAsync(string tokenHash);
    Task<User?> GetByEmailVerificationTokenAsync(string tokenHash);
    Task<(List<User> Users, int Total)> GetPagedAsync(UserQueryDto query);

    /// <summary>Tìm user bất kể IsActive — dùng cho Admin.</summary>
    Task<User?> GetByIdAnyStatusAsync(Guid id);

    /// <summary>Tìm user theo email bất kể IsActive — dùng để phân biệt "không tồn tại" vs "bị disable".</summary>
    Task<User?> GetByEmailAnyStatusAsync(string email);

    /// <summary>Tìm user theo GoogleId bất kể IsActive.</summary>
    Task<User?> GetByGoogleIdAnyStatusAsync(string googleId);

    /// <summary>Tìm user theo GithubId bất kể IsActive.</summary>
    Task<User?> GetByGithubIdAnyStatusAsync(string githubId);

    /// <summary>Load Role navigation property.</summary>
    Task LoadRoleAsync(User user);
}
