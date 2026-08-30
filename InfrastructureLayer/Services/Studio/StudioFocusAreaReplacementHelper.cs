using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Studio;
using InfrastructureLayer.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Services.Studio;

/// <summary>
/// Thay focus areas: soft-delete bằng ExecuteUpdate + insert qua DbSet (không đụng navigation/tracker cũ).
/// </summary>
internal static class StudioFocusAreaReplacementHelper
{
    /// <summary>
    /// Gọi sau khi StudioSettings đã có Id bền vững (đã SaveChanges nếu mới tạo).
    /// Không gắn vào settings.FocusAreas navigation — tránh concurrency 0-row.
    /// </summary>
    public static async Task ReplaceViaDbSetAsync(
        AppDbContext dbContext,
        Guid studioSettingsId,
        IReadOnlyList<StudioFocusAreaItemDto> focusAreas,
        CancellationToken ct)
    {
        if (studioSettingsId == Guid.Empty)
            throw new InvalidOperationException("StudioSettingsId trống — cần persist settings trước khi thay focus.");
        if (focusAreas.Count == 0) return;

        // Gỡ mọi focus đang track (nếu có) trước ExecuteUpdate
        foreach (var entry in dbContext.ChangeTracker.Entries<StudioFocusArea>()
                     .Where(e => e.Entity.StudioSettingsId == studioSettingsId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        await dbContext.StudioFocusAreas
            .Where(x => x.StudioSettingsId == studioSettingsId && x.IsActive)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(f => f.IsActive, false)
                    .SetProperty(f => f.UpdatedAt, DateTime.UtcNow),
                ct);

        var order = 0;
        var toAdd = new List<StudioFocusArea>();
        foreach (var item in focusAreas.OrderBy(x => x.OrderIndex))
        {
            if (string.IsNullOrWhiteSpace(item.Name)) continue;
            var name = item.Name.Trim();
            toAdd.Add(new StudioFocusArea
            {
                StudioSettingsId = studioSettingsId,
                Name = name[..Math.Min(name.Length, 150)],
                Weight = StudioFocusAreaWeightHelper.NormalizeToPercent(item.Weight),
                Description = item.Description,
                SourceReason = item.SourceReason,
                OrderIndex = item.OrderIndex > 0 ? item.OrderIndex : order,
                IsActive = true
            });
            order++;
        }

        if (toAdd.Count > 0)
            await dbContext.StudioFocusAreas.AddRangeAsync(toAdd, ct);

        await dbContext.SaveChangesAsync(ct);
    }
}
