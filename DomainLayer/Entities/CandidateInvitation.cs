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

    /// <summary>Lời nhắn candidate gửi kèm khi ACCEPTED — null nếu chưa phản hồi/từ chối hoặc không nhập.</summary>
    public string? ResponseMessage { get; set; }

    /// <summary>SĐT candidate chủ động chia sẻ khi ACCEPTED (candidate tự chọn có nhập hay không) — null nếu chưa phản hồi/từ chối hoặc không nhập.</summary>
    public string? SharedPhoneNumber { get; set; }

    public DateTime? ScheduledAtUtc { get; set; }
    public string? TimeZoneId { get; set; }
    public string? MeetingMode { get; set; }
    public string? MeetingLink { get; set; }
    public string? Location { get; set; }

    public CandidateRecommendation Recommendation { get; set; } = null!;
}
