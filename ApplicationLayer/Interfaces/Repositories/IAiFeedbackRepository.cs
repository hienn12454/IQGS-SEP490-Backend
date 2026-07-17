using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IAiFeedbackRepository
{
    Task<AiFeedback?> GetByCandidateAnswerIdAsync(Guid candidateAnswerId);
    Task AddAsync(AiFeedback feedback);
    Task UpdateAsync(AiFeedback feedback);

    /// <summary>Lấy toàn bộ feedback của các answer thuộc 1 practice session.</summary>
    Task<IReadOnlyList<AiFeedback>> GetBySessionIdAsync(Guid practiceSessionId);

    /// <summary>Trung bình Score của các feedback Succeeded trong session (null nếu chưa có).</summary>
    Task<double?> GetAverageSucceededScoreAsync(Guid practiceSessionId);
}
