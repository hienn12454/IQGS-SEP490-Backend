using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class PlatformSettingsRepository : IPlatformSettingsRepository
{
    private readonly Database.AppDbContext _context;

    public PlatformSettingsRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    // Luôn có đúng 1 dòng nhờ seed migration (PlatformSettings.SingletonId) — FirstAsync an toàn,
    // không cần null-check/tự tạo mới.
    public Task<PlatformSettings> GetAsync()
        => _context.PlatformSettings.FirstAsync();

    public async Task UpdateAsync(PlatformSettings settings)
    {
        _context.PlatformSettings.Update(settings);
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
