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
    public DateTime? PublishedAt { get; set; }
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
    public DateTime? PublishedAt { get; set; }
    public List<QuestionSetQuestionResponseDto> Questions { get; set; } = new();
}

public class QuestionSetActionResponseDto
{
    public Guid QuestionSetId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
}

/// <summary>Read-model cho 1 dòng marketplace (projection JOIN question_sets/HRProfiles/Companies), dùng nội bộ bởi repository.</summary>
public class PublishedQuestionSetRow
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogo { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string SkillsJson { get; set; } = "[]";
    public int TotalQuestions { get; set; }

    /// <summary>Mô tả bộ câu hỏi hiển thị trên card — lấy từ question_sets.HrNote.</summary>
    public string? Description { get; set; }

    /// <summary>Điểm trung bình OverallScore các phiên luyện tập đã chấm — null khi chưa có phiên nào được chấm.</summary>
    public double? Rating { get; set; }

    /// <summary>Số phiên luyện tập đã tạo trên bộ này.</summary>
    public int AttemptCount { get; set; }
}

/// <summary>Read-model chi tiết 1 bộ đã publish, dùng nội bộ bởi repository — KHÔNG chứa SampleAnswer/EvaluationCriteria.</summary>
public class PublishedQuestionSetDetail
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogo { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string SkillsJson { get; set; } = "[]";
    public string? Description { get; set; }
    public double? Rating { get; set; }
    public int AttemptCount { get; set; }
    public List<PublishedQuestionRow> Questions { get; set; } = new();
}

/// <summary>Read-model 1 câu hỏi trong bộ đã publish — cố tình KHÔNG có SampleAnswer/EvaluationCriteriaJson.</summary>
public class PublishedQuestionRow
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string Question { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Skill { get; set; }
    public string? FocusArea { get; set; }
    public string? Rationale { get; set; }
    public string CitationsJson { get; set; } = "[]";
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
