using ApplicationLayer.DTOs.Profile;

namespace ApplicationLayer.Interfaces;

/// <summary>SCRUM-161: Xem và cập nhật hồ sơ cá nhân.</summary>
public interface IProfileService
{
    Task<ProfileResponseDto> GetProfileAsync(Guid userId);
    Task<ProfileResponseDto> UpdateHRProfileAsync(Guid userId, UpdateHRProfileDto dto);
    Task<ProfileResponseDto> UpdateCandidateProfileAsync(Guid userId, UpdateCandidateProfileDto dto);
    Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
}
