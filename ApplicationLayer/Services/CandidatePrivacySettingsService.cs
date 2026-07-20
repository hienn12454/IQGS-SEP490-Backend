using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Entities;

namespace ApplicationLayer.Services;

/// <summary>Cài đặt quyền riêng tư của Candidate (SCRUM-293) — hiện gồm consent recommendation.</summary>
public class CandidatePrivacySettingsService : ICandidatePrivacySettingsService
{
    private readonly ICandidateProfileRepository _profileRepository;

    public CandidatePrivacySettingsService(ICandidateProfileRepository profileRepository)
    {
        _profileRepository = profileRepository;
    }

    public async Task<CandidatePrivacySettingsDto> GetAsync(Guid candidateUserId)
    {
        var profile = await _profileRepository.GetByUserIdAsync(candidateUserId);
        return new CandidatePrivacySettingsDto
        {
            AllowRecruiterRecommendation = profile?.AllowRecruiterRecommendation ?? false
        };
    }

    public async Task<CandidatePrivacySettingsDto> UpdateAsync(Guid candidateUserId, CandidatePrivacySettingsDto dto)
    {
        var profile = await _profileRepository.GetByUserIdAsync(candidateUserId);
        if (profile is null)
        {
            // Candidate chưa từng cập nhật hồ sơ — tạo profile tối thiểu để lưu consent.
            await _profileRepository.AddAsync(new CandidateProfile
            {
                UserId = candidateUserId,
                AllowRecruiterRecommendation = dto.AllowRecruiterRecommendation
            });
        }
        else
        {
            profile.AllowRecruiterRecommendation = dto.AllowRecruiterRecommendation;
            profile.UpdatedAt = DateTime.UtcNow;
            await _profileRepository.UpdateAsync(profile);
        }

        return new CandidatePrivacySettingsDto
        {
            AllowRecruiterRecommendation = dto.AllowRecruiterRecommendation
        };
    }
}
