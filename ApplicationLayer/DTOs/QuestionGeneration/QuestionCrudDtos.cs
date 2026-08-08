namespace ApplicationLayer.DTOs.QuestionGeneration;

public class UpdateQuestionRequestDto
{
    public string Question { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Skill { get; set; }
    public string? FocusArea { get; set; }
    public string? Rationale { get; set; }
    public string? SampleAnswer { get; set; }
    /// <summary>SCRUM-400: bắt buộc Text | Code.</summary>
    public string AnswerMethod { get; set; } = string.Empty;
    public List<object> EvaluationCriteria { get; set; } = new();
    public List<object> Citations { get; set; } = new();
}

public class CreateQuestionRequestDto
{
    public string Question { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Skill { get; set; }
    public string? FocusArea { get; set; }
    public string? Rationale { get; set; }
    public string? SampleAnswer { get; set; }
    /// <summary>SCRUM-400: bắt buộc Text | Code.</summary>
    public string AnswerMethod { get; set; } = string.Empty;
    public List<object> EvaluationCriteria { get; set; } = new();
    public List<object> Citations { get; set; } = new();
    public int? Order { get; set; }
}

public class ReorderQuestionItemDto
{
    public Guid QuestionId { get; set; }
    public int Order { get; set; }
}

public class ReorderQuestionsRequestDto
{
    public List<ReorderQuestionItemDto> Items { get; set; } = new();
}
