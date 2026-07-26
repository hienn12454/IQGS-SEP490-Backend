using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Recommendation;

/// <summary>Body gửi lời đề nghị phỏng vấn (offline) qua email cho candidate.</summary>
public class SendCandidateOfferRequestDto
{
    [Required(ErrorMessage = "Vui lòng nhập nội dung lời đề nghị.")]
    [MaxLength(5000, ErrorMessage = "Nội dung tối đa 5000 ký tự.")]
    public string Message { get; set; } = string.Empty;
}

public class SendCandidateOfferResponseDto
{
    public Guid RecommendationId { get; set; }
    public Guid OfferId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
