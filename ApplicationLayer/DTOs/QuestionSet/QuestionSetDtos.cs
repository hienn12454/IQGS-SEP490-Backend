namespace ApplicationLayer.DTOs.QuestionSet;

public class SaveDraftResponseDto
{
    public Guid QuestionSetId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid SourceJobId { get; set; }
    public int QuestionCount { get; set; }
    public DateTime SavedAt { get; set; }
}

public class QuestionSetDetailResponseDto
{
    public Guid QuestionSetId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid SourceJobId { get; set; }
    public string? Title { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public string? HrNote { get; set; }
    public object? Plan { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public DateTime SavedAt { get; set; }
    public List<QuestionSetQuestionResponseDto> Questions { get; set; } = new();
}

public class QuestionSetQuestionResponseDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string Question { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Skill { get; set; }
    public string? FocusArea { get; set; }
    public string? Rationale { get; set; }
    public string? SampleAnswer { get; set; }
    public List<object> EvaluationCriteria { get; set; } = new();
    public List<object> Citations { get; set; } = new();
}
