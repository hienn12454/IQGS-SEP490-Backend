using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.Interfaces.Services;

public interface ICandidatePrivacySettingsService
{
    Task<CandidatePrivacySettingsDto> GetAsync(Guid candidateUserId);
    Task<CandidatePrivacySettingsDto> UpdateAsync(Guid candidateUserId, CandidatePrivacySettingsDto dto);
}
