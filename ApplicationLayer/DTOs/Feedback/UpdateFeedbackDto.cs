using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Feedback;

public class UpdateFeedbackDto
{
    [Required(ErrorMessage = "Nội dung feedback là bắt buộc.")]
    [MinLength(10, ErrorMessage = "Nội dung feedback phải có ít nhất 10 ký tự.")]
    [MaxLength(1000, ErrorMessage = "Nội dung feedback không được vượt quá 1000 ký tự.")]
    public string Content { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao.")]
    public int? Rating { get; set; }
}
