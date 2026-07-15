using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Candidate;

public class SubmitAnswerDto
{
    [Required(ErrorMessage = "questionId là bắt buộc.")]
    public Guid QuestionId { get; set; }

    [Required(ErrorMessage = "Câu trả lời không được để trống.")]
    [MinLength(1, ErrorMessage = "Câu trả lời không được để trống.")]
    public string AnswerText { get; set; } = string.Empty;
}

public class SubmitAnswerResponseDto
{
    public Guid QuestionId { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}
