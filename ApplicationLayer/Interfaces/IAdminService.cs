using ApplicationLayer.DTOs.Admin;

namespace ApplicationLayer.Interfaces;

/// <summary>SCRUM-163: Quản lý người dùng cho Admin.</summary>
public interface IAdminService
{
    Task<PagedResultDto<UserListItemDto>> GetUsersAsync(UserQueryDto query);
    Task<UserDetailDto> GetUserDetailAsync(Guid userId);
    Task UpdateUserStatusAsync(Guid userId, bool isActive);
}
