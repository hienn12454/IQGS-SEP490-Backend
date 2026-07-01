using ApplicationLayer.DTOs.Candidate;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IPracticeSessionRepository
{
    Task AddAsync(PracticeSession session);
    Task<PracticeSession?> GetByIdAsync(Guid id);
    /// <summary>Phiên mới nhất của candidate trên 1 bộ câu hỏi (mọi trạng thái) — mỗi bộ chỉ giữ 1 phiên, luyện lại sẽ ghi đè phiên này.</summary>
    Task<PracticeSession?> GetLatestByQuestionSetAsync(Guid candidateUserId, Guid questionSetId);
    Task UpdateAsync(PracticeSession session);

    Task<(IReadOnlyList<PracticeSessionRow> Items, int TotalCount)> ListAsync(
        Guid candidateUserId, string? status, Guid? questionSetId, string? keyword,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize);

    Task<PracticeSessionStatsDto> GetStatsAsync(Guid candidateUserId, DateTime? fromDate, DateTime? toDate);
}
