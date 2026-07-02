using ApplicationLayer.DTOs.Candidate;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IPracticeSessionRepository
{
    Task AddAsync(PracticeSession session);
    Task<PracticeSession?> GetByIdAsync(Guid id);
    Task<PracticeSession?> GetInProgressByQuestionSetAsync(Guid candidateUserId, Guid questionSetId);
    Task UpdateAsync(PracticeSession session);

    Task<(IReadOnlyList<PracticeSessionRow> Items, int TotalCount)> ListAsync(
        Guid candidateUserId, string? status, Guid? questionSetId, string? keyword,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize);

    Task<PracticeSessionStatsDto> GetStatsAsync(Guid candidateUserId, DateTime? fromDate, DateTime? toDate);
}
