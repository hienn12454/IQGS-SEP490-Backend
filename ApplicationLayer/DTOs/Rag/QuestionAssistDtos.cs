namespace ApplicationLayer.DTOs.Rag;

public class QuestionAssistChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class QuestionAssistContextDto
{
    public string JobDescription { get; set; } = string.Empty;
    public string? HrNote { get; set; }
    public object? Plan { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public object CurrentQuestion { get; set; } = new();
    public bool IsManuallyEdited { get; set; }
}

public class QuestionAssistRequest
{
    public Guid OwnerId { get; set; }
    public string HrMessage { get; set; } = string.Empty;
    public QuestionAssistContextDto Context { get; set; } = new();
    public List<QuestionAssistChatMessageDto> ChatHistory { get; set; } = new();
}

public class QuestionAssistSuggestionDto
{
    public string? Question { get; set; }
    public string? Rationale { get; set; }
    public string? SampleAnswer { get; set; }
    public string? Difficulty { get; set; }
    public string? QuestionType { get; set; }
}

public class QuestionAssistResult
{
    public bool Success { get; set; }
    public string Reply { get; set; } = string.Empty;
    public QuestionAssistSuggestionDto? Suggestion { get; set; }
    public double? ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
}
