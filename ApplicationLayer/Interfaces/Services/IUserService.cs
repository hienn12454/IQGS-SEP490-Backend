using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.Profile;

namespace ApplicationLayer.Interfaces.Services;

public interface IUserService
{
    // ── Admin operations ──────────────────────────────────────────────
    Task<PagedResultDto<UserListItemDto>> GetUsersAsync(UserQueryDto query);
    /// <summary>SCRUM-480: thống kê user theo role + status.</summary>
    Task<AdminUserStatsDto> GetUserStatsAsync();
    Task<UserDetailDto> GetUserDetailAsync(Guid userId);
    Task UpdateUserStatusAsync(Guid userId, bool isActive);

    // ── Self (me) operations ──────────────────────────────────────────
    Task<ProfileResponseDto> GetProfileAsync(Guid userId);
    Task<ProfileResponseDto> UpdateHRProfileAsync(Guid userId, UpdateHRProfileDto dto);
    Task<ProfileResponseDto> UpdateCandidateProfileAsync(Guid userId, UpdateCandidateProfileDto dto);
    Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
}
