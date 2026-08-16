using ApplicationLayer.DTOs.Candidate;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Services;

public interface ICandidateSkillPlanService
{
    Task<CandidateSkillPlanDto?> GetAsync(Guid candidateUserId);
    Task UpsertFromSessionAsync(PracticeSession session, CandidatePersonalSetJob job);
}
