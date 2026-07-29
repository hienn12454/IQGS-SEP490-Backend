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
    /// <summary>Cấp độ ứng viên phù hợp: intern | junior | mid | senior | lead</summary>
    public string ExperienceLevel { get; set; } = string.Empty;
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

public class JobInputSnapshotDto
{
    public string JobDescription { get; set; } = string.Empty;
    public string JobDescriptionPreview { get; set; } = string.Empty;
    public string? HrNote { get; set; }
    public int NumberOfQuestions { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public List<string> QuestionTypes { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public string JdInputType { get; set; } = string.Empty;
    public string? JdFileName { get; set; }
}

public class JobMetaDto
{
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int QuestionCount { get; set; }
    public bool HasDraft { get; set; }
    public bool PlanApproved { get; set; }
    public Guid? QuestionSetId { get; set; }
    /// <summary>SCRUM-374: job mirror từ Studio v2.</summary>
    public bool IsFromStudio { get; set; }
}

public class JobFailureDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Stage { get; set; }
    public string? Source { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class JobUiActionsDto
{
    public bool CanPoll { get; set; }
    public bool CanEditInput { get; set; }
    public bool CanRetryPlan { get; set; }
    public bool CanRetryQuestions { get; set; }
    public bool CanEditPlan { get; set; }
    public bool CanApprovePlan { get; set; }
    public bool CanViewQuestions { get; set; }
    public bool CanEditQuestions { get; set; }
    public bool CanSaveDraft { get; set; }
    public bool CanViewDraft { get; set; }
    public bool CanEditDraftQuestions { get; set; }
}

public class JobUiStateDto
{
    public string StatusLabel { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public bool IsPolling { get; set; }
    public string SuggestedAction { get; set; } = string.Empty;
    public JobUiActionsDto Actions { get; set; } = new();
}

/// <summary>Tóm tắt field chính để FE hiển thị — merge input HR + plan LLM khi có.</summary>
public class JobStatusSummaryDto
{
    public string? Role { get; set; }
    /// <summary>Độ khó câu hỏi: easy | medium | hard</summary>
    public string Level { get; set; } = string.Empty;
    /// <summary>Cấp ứng viên phù hợp: intern | junior | mid | senior | lead</summary>
    [System.Text.Json.Serialization.JsonPropertyName("experience_level")]
    public string? ExperienceLevel { get; set; }
    public int NumberOfQuestions { get; set; }
    public List<string> QuestionTypes { get; set; } = new();
    public List<string> Skills { get; set; } = new();
}

/// <summary>Plan JSON đã sinh — typed để Swagger hiển thị schema.</summary>
public class JobPlanResponseDto
{
    public string RoleTitle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Level { get; set; }
    public string? ExperienceLevel { get; set; }
    public int TotalQuestions { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<object> QuestionTypeDistribution { get; set; } = new();
    public List<object> DifficultyDistribution { get; set; } = new();
    public List<object> Coverage { get; set; } = new();
    public List<object> RecommendedQuestionOutline { get; set; } = new();
    public string? Notes { get; set; }
    public List<object> Citations { get; set; } = new();
}

public class JobStatusResponseDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public JobInputSnapshotDto Input { get; set; } = new();
    public JobStatusSummaryDto Summary { get; set; } = new();
    /// <summary>Cấp ứng viên phù hợp (mirror từ summary/plan).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("experience_level")]
    public string? ExperienceLevel { get; set; }
    public JobPlanResponseDto? Plan { get; set; }
    public JobMetaDto Meta { get; set; } = new();
    public JobUiStateDto Ui { get; set; } = new();
    public JobFailureDto? Failure { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class JobQuestionsResponseDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<GeneratedQuestionResponseDto> Questions { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public StructuredErrorResponseDto? Error { get; set; }
}

public class GeneratedQuestionResponseDto
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

public class CreatePlanJobResponseDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string JdInputType { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    /// <summary>Vị trí tuyển dụng — null khi job mới queue, có sau khi plan sinh xong.</summary>
    public string? Role { get; set; }
    public string Level { get; set; } = string.Empty;
    /// <summary>Cấp độ ứng viên phù hợp — null khi job mới queue, có sau khi plan sinh xong.</summary>
    public string? ExperienceLevel { get; set; }
    public int NumberOfQuestions { get; set; }
    public List<string> QuestionTypes { get; set; } = new();
    public List<string> Skills { get; set; } = new();
}
