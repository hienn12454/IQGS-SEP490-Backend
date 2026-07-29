namespace DomainLayer.Entities;

/// <summary>
/// Lời đề nghị phỏng vấn (offline) HR gửi cho candidate qua email, gắn với 1 recommendation.
/// KHÔNG 1-1 với CandidateRecommendation như CandidateInvitation — 1 recommendation có thể có nhiều
/// CandidateOffer theo thời gian (HR gửi lại), miễn lần gần nhất chưa ACCEPTED. Độc lập hoàn toàn với
/// luồng CandidateInvitation trong app — không đọc/ghi CandidateRecommendation.Status.
/// </summary>
public class CandidateOffer : BaseEntity
{
    public Guid RecommendationId { get; set; }

    /// <summary>HR gửi offer (denormalize từ recommendation để query nhanh).</summary>
    public Guid HrUserId { get; set; }

    /// <summary>Candidate nhận offer (denormalize từ recommendation để query nhanh).</summary>
    public Guid CandidateUserId { get; set; }

    /// <summary>Nội dung offer tự do do HR nhập — hiển thị lại nguyên văn trên trang xác nhận + trong email.</summary>
    public string Message { get; set; } = string.Empty;

    public string Status { get; set; } = Constants.CandidateOfferStatus.Sent;

    /// <summary>SHA-256 hash (hex, 64 ký tự) của raw accept token — không lưu raw token (cùng recipe PasswordResetToken).</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime TokenExpiresAt { get; set; }

    /// <summary>Thời điểm candidate bấm xác nhận (POST) — null nếu chưa accept.</summary>
    public DateTime? AcceptedAt { get; set; }

    public CandidateRecommendation Recommendation { get; set; } = null!;
}
