using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

/// <summary>Đọc/ghi bảng platform_settings — luôn đúng 1 dòng (seed sẵn qua migration).</summary>
public interface IPlatformSettingsRepository
{
    Task<PlatformSettings> GetAsync();
    Task UpdateAsync(PlatformSettings settings);
}
