using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface ICandidatePersonalSetJobRepository
{
    Task AddAsync(CandidatePersonalSetJob job);
    Task UpdateAsync(CandidatePersonalSetJob job);
    Task<CandidatePersonalSetJob?> GetByIdAsync(Guid id);
    Task<CandidatePersonalSetJob?> GetByQuestionSetIdAsync(Guid questionSetId);
    Task<IReadOnlyList<CandidatePersonalSetJob>> ListByCandidateAsync(Guid candidateUserId);
}
