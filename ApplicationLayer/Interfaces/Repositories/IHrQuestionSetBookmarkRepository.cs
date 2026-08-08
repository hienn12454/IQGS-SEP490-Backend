using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

/// <summary>SCRUM-324: HR bookmark bộ câu hỏi của chính mình.</summary>
public interface IHrQuestionSetBookmarkRepository
{
    Task<HrQuestionSetBookmark?> GetAsync(Guid hrUserId, Guid questionSetId);
    Task AddAsync(HrQuestionSetBookmark bookmark);
    Task DeleteAsync(HrQuestionSetBookmark bookmark);
    Task<IReadOnlyList<QuestionSet>> ListBookmarkedQuestionSetsAsync(Guid hrUserId);
    /// <summary>SCRUM-391: id các set HR đã bookmark (cho list History).</summary>
    Task<HashSet<Guid>> GetBookmarkedQuestionSetIdsAsync(Guid hrUserId);
}
