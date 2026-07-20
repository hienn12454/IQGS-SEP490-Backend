namespace DomainLayer.Entities;

/// <summary>
/// Lời mời phỏng vấn HR gửi cho candidate từ 1 recommendation (SCRUM-295).
/// 1-1 với CandidateRecommendation — mỗi recommendation chỉ mời được 1 lần.
/// </summary>
public class CandidateInvitation : BaseEntity
{
    public Guid RecommendationId { get; set; }

    /// <summary>HR gửi lời mời (denormalize từ recommendation để query nhanh).</summary>
    public Guid HrUserId { get; set; }

    /// <summary>Candidate nhận lời mời (denormalize từ recommendation để query nhanh).</summary>
    public Guid CandidateUserId { get; set; }

    public string? Message { get; set; }
    public string Status { get; set; } = Constants.InvitationStatus.Pending;
    public DateTime? RespondedAt { get; set; }

    public CandidateRecommendation Recommendation { get; set; } = null!;
}
