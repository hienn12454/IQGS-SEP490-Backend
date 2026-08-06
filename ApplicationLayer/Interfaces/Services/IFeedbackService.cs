using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.Feedback;

namespace ApplicationLayer.Interfaces.Services;

public interface IFeedbackService
{
    Task<FeedbackDto> CreateAsync(Guid userId, CreateFeedbackDto dto);
    Task<List<FeedbackDto>> GetPublicApprovedAsync(int limit);
    Task<PagedResultDto<FeedbackDto>> GetPagedForAdminAsync(FeedbackQueryDto query);
    Task<FeedbackDto> UpdateAsync(Guid id, UpdateFeedbackDto dto);
    Task<FeedbackDto> UpdateStatusAsync(Guid id, string status);
    Task DeleteAsync(Guid id);
}
