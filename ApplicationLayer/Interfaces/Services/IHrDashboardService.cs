using ApplicationLayer.DTOs.Hr;

namespace ApplicationLayer.Interfaces.Services;

public interface IHrDashboardService
{
    Task<HrDashboardResponseDto> GetDashboardAsync(Guid hrUserId, HrDashboardQueryDto query);
}
