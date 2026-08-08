using ApplicationLayer.DTOs.Admin;

namespace ApplicationLayer.Interfaces.Services;

/// <summary>SCRUM-404: quản lý Marketplace phía Admin.</summary>
public interface IAdminMarketplaceService
{
    Task<PagedResultDto<AdminMarketplaceListItemDto>> ListAsync(AdminMarketplaceListQueryDto query);
    Task<AdminMarketplaceDetailDto> GetByIdAsync(Guid id);
    Task<AdminMarketplacePinResultDto> PinAsync(Guid id);
    Task<AdminMarketplacePinResultDto> UnpinAsync(Guid id);
    Task<AdminMarketplaceStatsDto> GetStatsAsync();
}
