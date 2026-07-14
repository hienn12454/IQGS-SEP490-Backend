namespace ApplicationLayer.DTOs.QuestionGeneration;

public class AskQuestionAiRequestDto
{
    public string Message { get; set; } = string.Empty;
    /// <summary>FE override tùy chọn — field trống/null thì BE dùng nguồn mặc định.</summary>
    public CurrentQuestionOverrideDto? CurrentQuestion { get; set; }
}

public class CurrentQuestionOverrideDto
{
    public string? Question { get; set; }
    public string? QuestionType { get; set; }
    public string? Difficulty { get; set; }
    public string? Skill { get; set; }
    public string? FocusArea { get; set; }
    public string? Rationale { get; set; }
    public string? SampleAnswer { get; set; }
}

public class QuestionAiSuggestionDto
{
    public string? Question { get; set; }
    public string? Rationale { get; set; }
    public string? SampleAnswer { get; set; }
    public string? Difficulty { get; set; }
    public string? QuestionType { get; set; }
}

public class QuestionAiChatMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public QuestionAiSuggestionDto? Suggestion { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AskQuestionAiResponseDto
{
    public string Reply { get; set; } = string.Empty;
    public QuestionAiSuggestionDto? Suggestion { get; set; }
}

public class QuestionAiChatHistoryResponseDto
{
    public Guid JobId { get; set; }
    public Guid QuestionId { get; set; }
    public List<QuestionAiChatMessageDto> Messages { get; set; } = new();
}
