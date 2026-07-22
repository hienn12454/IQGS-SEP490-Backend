using ApplicationLayer.DTOs.Admin;

namespace ApplicationLayer.Interfaces.Services;

/// <summary>Cấu hình toàn hệ thống do Admin chỉnh runtime (vd số câu hỏi tối thiểu để publish) — không cần redeploy.</summary>
public interface IPlatformSettingsService
{
    Task<PlatformSettingsDto> GetAsync();
    Task<PlatformSettingsDto> UpdateAsync(UpdatePlatformSettingsDto dto);
}
