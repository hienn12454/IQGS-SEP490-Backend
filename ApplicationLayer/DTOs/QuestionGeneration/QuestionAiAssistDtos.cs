namespace ApplicationLayer.DTOs.QuestionGeneration;

public class AskQuestionAiRequestDto
{
    public string Message { get; set; } = string.Empty;
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
