namespace ApplicationLayer.DTOs.Rag;

public class RagIngestRequest
{
    public Guid DocumentId { get; set; }
    public string BlobReadUrl { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? SourceTitle { get; set; }
}

public class RagIngestResult
{
    public Guid DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ChunkCount { get; set; }
    public string? ErrorMessage { get; set; }
}

public class RagAsyncAcceptedResult
{
    public bool Accepted { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? JobId { get; set; }
    public string Phase { get; set; } = string.Empty;
}

public class RagDeleteResult
{
    public Guid DocumentId { get; set; }
    public int DeletedCount { get; set; }
}

public class ValidateJdRequest
{
    public string JobDescription { get; set; } = string.Empty;
    public string? FileName { get; set; }
}

public class ValidateJdResult
{
    public bool Success { get; set; }
    public string? JobDescription { get; set; }
    public string? FileName { get; set; }
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object>? Stats { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ParseJdResult
{
    public bool Success { get; set; }
    public string? JobDescription { get; set; }
    public string? FileName { get; set; }
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object>? Stats { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ParseCvResult
{
    public bool Success { get; set; }
    public List<string> Skills { get; set; } = new();
    public string? Summary { get; set; }
    public string? FileName { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class GeneratePlanRequest
{
    public Guid OwnerId { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public int NumberOfQuestions { get; set; }
    public string Difficulty { get; set; } = "medium";
    public List<string> QuestionTypes { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public string? HrNote { get; set; }
}

public class GeneratePlanResult
{
    public bool Success { get; set; }
    public object? Plan { get; set; }
    public double? ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class GenerateQuestionsFromPlanRequest
{
    public Guid OwnerId { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public object ApprovedPlan { get; set; } = new();
    public string? HrNote { get; set; }
}

public class RagGeneratedQuestionDto
{
    public string Question { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Rationale { get; set; }
    public string? SampleAnswer { get; set; }
    public List<object>? Citations { get; set; }
    public int? Order { get; set; }
    public string? Skill { get; set; }
    public string? FocusArea { get; set; }
    public List<string>? EvaluationCriteria { get; set; }
}

public class GenerateQuestionsFromPlanResult
{
    public bool Success { get; set; }
    public List<RagGeneratedQuestionDto> Questions { get; set; } = new();
    public double? ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
}
