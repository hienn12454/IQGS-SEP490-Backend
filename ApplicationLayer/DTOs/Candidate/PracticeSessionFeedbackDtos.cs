namespace ApplicationLayer.DTOs.Candidate;

/// <summary>Response GET feedback — align FE AnswerRecord / PracticeSession (SCRUM-282 / SCRUM-283).</summary>
public class PracticeSessionFeedbackDto
{
    public Guid SessionId { get; set; }
    public double? OverallScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<PracticeSessionFeedbackItemDto> Items { get; set; } = new();
}

public class PracticeSessionFeedbackItemDto
{
    public Guid QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string AnswerText { get; set; } = string.Empty;
    public double? Score { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public string? Suggestion { get; set; }
    public Dictionary<string, double>? DimensionScores { get; set; }
    public string EvaluationStatus { get; set; } = string.Empty;
}
