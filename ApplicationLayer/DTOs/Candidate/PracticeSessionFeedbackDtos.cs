namespace ApplicationLayer.DTOs.Candidate;

/// <summary>Response GET feedback — align FE AnswerRecord / PracticeSession (SCRUM-282 / SCRUM-283 / SCRUM-305 / SCRUM-332).</summary>
public class PracticeSessionFeedbackDto
{
    public Guid SessionId { get; set; }
    public double? OverallScore { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>FreeTeaser | Full — FE blur/upsell theo flag server (không tin client).</summary>
    public string AccessLevel { get; set; } = string.Empty;

    /// <summary>AI Insight tổng quan + kỹ năng cần cải thiện (SCRUM-305). Free teaser thường null.</summary>
    public PracticeAiInsightDto? AiInsight { get; set; }

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

    /// <summary>True = Free chưa mở chi tiết — FE blur + upsell.</summary>
    public bool IsLocked { get; set; }

    /// <summary>True = câu mẫu Free được mở khóa.</summary>
    public bool IsTeaser { get; set; }
}
