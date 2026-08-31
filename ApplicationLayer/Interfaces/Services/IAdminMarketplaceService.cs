using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.QuestionSet;

namespace ApplicationLayer.Interfaces.Services;

/// <summary>SCRUM-404: quản lý Marketplace phía Admin.</summary>
public interface IAdminMarketplaceService
{
    Task<PagedResultDto<AdminMarketplaceListItemDto>> ListAsync(AdminMarketplaceListQueryDto query);
    Task<AdminMarketplaceDetailDto> GetByIdAsync(Guid id);
    Task<AdminMarketplacePinResultDto> PinAsync(Guid id);
    Task<AdminMarketplacePinResultDto> UnpinAsync(Guid id);
    /// <summary>UC65: Admin gỡ bộ khỏi Marketplace — không check owner HR.</summary>
    Task<QuestionSetActionResponseDto> UnpublishAsync(Guid id);
    Task<AdminMarketplaceStatsDto> GetStatsAsync();
}
