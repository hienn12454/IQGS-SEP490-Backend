using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Hr;

namespace ApplicationLayer.Interfaces.Services;

public interface IHrTalentService
{
    Task<PagedResultDto<HrTalentItemDto>> ListAsync(Guid hrUserId, HrTalentListQueryDto query);
}
