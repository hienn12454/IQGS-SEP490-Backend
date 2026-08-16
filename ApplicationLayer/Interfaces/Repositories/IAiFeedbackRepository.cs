using ApplicationLayer.DTOs.Recommendation;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IAiFeedbackRepository
{
    Task<AiFeedback?> GetByCandidateAnswerIdAsync(Guid candidateAnswerId);
    Task AddAsync(AiFeedback feedback);
    Task UpdateAsync(AiFeedback feedback);

    /// <summary>Lấy toàn bộ feedback của các answer thuộc 1 practice session.</summary>
    Task<IReadOnlyList<AiFeedback>> GetBySessionIdAsync(Guid practiceSessionId);

    /// <summary>
    /// Danh sách Score các feedback Succeeded (dùng tính overall = sum / tổng câu trong bộ — SCRUM-304).
    /// </summary>
    Task<IReadOnlyList<double>> GetSucceededScoresAsync(Guid practiceSessionId);

    /// <summary>
    /// Gamification — Improvement Bonus: điểm Succeeded gần nhất của CÙNG câu hỏi (questionSetQuestionId),
    /// từ 1 phiên COMPLETED KHÁC (loại trừ excludePracticeSessionId) của cùng candidate — null nếu candidate
    /// chưa từng hoàn thành câu này ở phiên nào khác trước đó.
    /// </summary>
    Task<double?> GetPreviousBestSucceededScoreAsync(
        Guid candidateUserId, Guid questionSetQuestionId, Guid excludePracticeSessionId);

    Task<IReadOnlyList<SkillScoreDto>> GetSkillAveragesBySessionAsync(Guid practiceSessionId);
}
