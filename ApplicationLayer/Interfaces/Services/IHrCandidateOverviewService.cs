using ApplicationLayer.DTOs.Hr;

namespace ApplicationLayer.Interfaces.Services;

/// <summary>SCRUM-398: HR xem overview candidate (stats + practice trên set của mình).</summary>
public interface IHrCandidateOverviewService
{
    Task<HrCandidateOverviewDto> GetOverviewAsync(Guid candidateUserId, Guid hrUserId);
}
