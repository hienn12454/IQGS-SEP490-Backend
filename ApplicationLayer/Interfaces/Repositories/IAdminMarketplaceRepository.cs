using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.QuestionSet;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

/// <summary>SCRUM-404: đọc/ghi quản lý Marketplace phía Admin.</summary>
public interface IAdminMarketplaceRepository
{
    Task<(IReadOnlyList<AdminMarketplaceSetRow> Items, int TotalCount)> ListPublishedAsync(
        int page, int pageSize, string? keyword, Guid? companyId, Guid? hrUserId, string sortBy);

    Task<AdminMarketplaceSetRow?> GetPublishedByIdAsync(Guid id);

    Task<IReadOnlyList<AdminMarketplaceQuestionSummaryDto>> GetQuestionSummariesAsync(Guid questionSetId);

    /// <summary>Danh sách practitioner — không lọc AllowRecruiterRecommendation (quyền Admin).</summary>
    Task<IReadOnlyList<QuestionSetPractitionerRow>> ListPractitionersForAdminAsync(Guid questionSetId);

    Task<QuestionSet?> GetByIdForUpdateAsync(Guid id);

    Task UpdateAsync(QuestionSet questionSet);

    Task<int> CountPinnedAsync();

    Task<AdminMarketplaceStatsDto> GetStatsAsync();
}
