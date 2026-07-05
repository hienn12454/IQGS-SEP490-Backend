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

    /// <summary>Succeeded | Failed — answer luôn được lưu kể cả khi evaluate fail (AC-03 SCRUM-282).</summary>
    public string EvaluationStatus { get; set; } = string.Empty;

    public double? Score { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public string? Suggestion { get; set; }
    public Dictionary<string, double>? DimensionScores { get; set; }
    public string? EvaluationError { get; set; }
}
