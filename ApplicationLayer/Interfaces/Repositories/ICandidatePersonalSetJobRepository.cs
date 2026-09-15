using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface ICandidatePersonalSetJobRepository
{
    Task AddAsync(CandidatePersonalSetJob job);
    Task UpdateAsync(CandidatePersonalSetJob job);
    Task<CandidatePersonalSetJob?> GetByIdAsync(Guid id);
    Task<CandidatePersonalSetJob?> GetByQuestionSetIdAsync(Guid questionSetId);
    /// <summary>Lookup scoring: gồm cả job IsActive=false — tránh miss AssessmentId sau archive.</summary>
    Task<CandidatePersonalSetJob?> GetByQuestionSetIdIncludingInactiveAsync(Guid questionSetId);
    Task<IReadOnlyList<CandidatePersonalSetJob>> ListByCandidateAsync(Guid candidateUserId);
}
