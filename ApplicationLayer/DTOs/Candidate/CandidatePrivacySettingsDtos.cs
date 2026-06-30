namespace ApplicationLayer.DTOs.Candidate;

/// <summary>Cài đặt quyền riêng tư của Candidate (SCRUM-293) — dùng chung cho GET response và PUT request.</summary>
public class CandidatePrivacySettingsDto
{
    /// <summary>Cho phép hệ thống đề xuất hồ sơ cho HR khi đạt điều kiện recommendation — mặc định false.</summary>
    public bool AllowRecruiterRecommendation { get; set; }
}
