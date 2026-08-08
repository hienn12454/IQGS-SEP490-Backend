using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Feedback;

public class UpdateFeedbackStatusDto
{
    [Required(ErrorMessage = "Trạng thái là bắt buộc.")]
    public string Status { get; set; } = string.Empty;
}
