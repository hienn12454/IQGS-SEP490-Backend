namespace ApplicationLayer.DTOs.QuestionSet;

/// <summary>Query danh sách question set — lọc theo job nguồn (tùy chọn).</summary>
public class QuestionSetListQueryDto
{
    /// <summary>Chỉ lấy question set thuộc generation job này. Bỏ trống = tất cả của HR.</summary>
    public Guid? JobId { get; set; }
}

/// <summary>Một question set kèm job nguồn.</summary>
public class QuestionSetListItemDto
{
    /// <summary>ID của bộ câu hỏi đã lưu draft.</summary>
    public Guid QuestionSetId { get; set; }

    /// <summary>ID generation session (job) đã tạo ra bộ câu hỏi này.</summary>
    public Guid JobId { get; set; }

    public string? Title { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; }
}

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
