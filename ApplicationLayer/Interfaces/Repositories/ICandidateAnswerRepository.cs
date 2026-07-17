using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface ICandidateAnswerRepository
{
    Task<CandidateAnswer?> GetAsync(Guid practiceSessionId, Guid questionSetQuestionId);
    Task AddAsync(CandidateAnswer answer);
    Task UpdateAsync(CandidateAnswer answer);
    Task<Dictionary<Guid, string>> GetAnswersBySessionIdAsync(Guid practiceSessionId);

    /// <summary>Toàn bộ answer entity của session — dùng khi build feedback response (SCRUM-282).</summary>
    Task<IReadOnlyList<CandidateAnswer>> GetEntitiesBySessionIdAsync(Guid practiceSessionId);
}
