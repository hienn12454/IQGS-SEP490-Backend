using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IQuestionSetFeedbackRepository
{
    /// <summary>Feedback của 1 candidate cho 1 question set — null nếu chưa gửi.</summary>
    Task<QuestionSetFeedback?> GetAsync(Guid candidateUserId, Guid questionSetId);

    Task AddAsync(QuestionSetFeedback feedback);
    Task UpdateAsync(QuestionSetFeedback feedback);

    /// <summary>Danh sách feedback (kèm CandidateUser) của 1 question set, phân trang, mới nhất trước.</summary>
    Task<(List<QuestionSetFeedback> Items, int TotalCount)> GetPagedByQuestionSetAsync(
        Guid questionSetId, int page, int pageSize);

    /// <summary>Rating trung bình + tổng số feedback của 1 question set.</summary>
    Task<(double? Average, int Count)> GetSummaryAsync(Guid questionSetId);
}
