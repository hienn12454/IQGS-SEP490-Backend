namespace ApplicationLayer.DTOs.QuestionGeneration;

public class CreatePlanJobRequestDto
{
    public string JobDescription { get; set; } = string.Empty;
    public string? HrNote { get; set; }
    public int NumberOfQuestions { get; set; }
    public string Difficulty { get; set; } = "medium";
    public List<string> QuestionTypes { get; set; } = new();
    public List<string> Skills { get; set; } = new();
}

public class UpdatePlanRequestDto
{
    public string RoleTitle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "medium";
    public int TotalQuestions { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<object> QuestionTypeDistribution { get; set; } = new();
    public List<object> DifficultyDistribution { get; set; } = new();
    public List<object> Coverage { get; set; } = new();
    public List<object> RecommendedQuestionOutline { get; set; } = new();
    public string? Notes { get; set; }
}

public class StructuredErrorResponseDto
{
    public string Error { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? Source { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class JobStatusResponseDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
    public string? HrNote { get; set; }
    public string JdInputType { get; set; } = string.Empty;
    public string? JdFileName { get; set; }
    public int NumberOfQuestions { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public List<string> QuestionTypes { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public object? Plan { get; set; }
    public string? ErrorMessage { get; set; }
    public StructuredErrorResponseDto? Error { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class JobQuestionsResponseDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<GeneratedQuestionResponseDto> Questions { get; set; } = new();
}

public class GeneratedQuestionResponseDto
{
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

public class CreatePlanJobResponseDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string JdInputType { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
}
