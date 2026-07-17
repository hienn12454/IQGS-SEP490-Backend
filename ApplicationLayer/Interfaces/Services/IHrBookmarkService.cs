using ApplicationLayer.DTOs.Hr;
using ApplicationLayer.DTOs.QuestionSet;

namespace ApplicationLayer.Interfaces.Services;

/// <summary>SCRUM-324: HR bookmark bộ câu hỏi của chính mình để mở lại nhanh.</summary>
public interface IHrBookmarkService
{
    Task<HrBookmarkToggleResponseDto> ToggleAsync(Guid questionSetId, Guid hrUserId);
    Task<IReadOnlyList<QuestionSetListItemDto>> ListBookmarkedAsync(Guid hrUserId);
}
