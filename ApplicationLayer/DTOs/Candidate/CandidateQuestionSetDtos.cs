namespace ApplicationLayer.DTOs.Candidate;

/// <summary>Query danh sách bộ câu hỏi public trên marketplace — hỗ trợ filter/search (SCRUM-268).</summary>
public class CandidateQuestionSetListQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Tìm theo title hoặc ghi chú HR (question_sets.Title, HrNote).</summary>
    public string? Keyword { get; set; }

    /// <summary>Chỉ lấy bộ thuộc công ty này.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Lọc theo độ khó của câu hỏi (question_set_questions.Difficulty) — bộ có ít nhất 1 câu khớp.</summary>
    public string? Difficulty { get; set; }

    /// <summary>Lọc theo kỹ năng của câu hỏi (question_set_questions.Skill), OR match — có thể truyền nhiều lần.</summary>
    public List<string>? Skills { get; set; }
}

/// <summary>1 item hiển thị trên card marketplace.</summary>
public class CandidateQuestionSetListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Id công ty — FE dùng cho chip filter companyId.</summary>
    public Guid CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogo { get; set; }

    /// <summary>Mô tả bộ câu hỏi (question_sets.HrNote) — null nếu HR không nhập.</summary>
    public string? Description { get; set; }

    public string Difficulty { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public int TotalQuestions { get; set; }

    /// <summary>Thời gian làm bài hiển thị trên card: HR có đặt giới hạn thì lấy giới hạn đó, chưa đặt thì ước tính theo số câu.</summary>
    public int EstimatedTimeMinutes { get; set; }

    /// <summary>Giới hạn thời gian làm bài HR đặt (phút) — null = không giới hạn, hết giờ hệ thống tự nộp bài.</summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>Điểm trung bình các phiên luyện tập đã chấm (1 chữ số thập phân) — null khi chưa có phiên nào được chấm.</summary>
    public double? Rating { get; set; }

    /// <summary>Số lượt luyện tập trên bộ này.</summary>
    public int AttemptCount { get; set; }
}

/// <summary>Chi tiết 1 bộ câu hỏi public — KHÔNG chứa sampleAnswer/evaluationCriteria (AC-04 SCRUM-269).</summary>
public class CandidateQuestionSetDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Id công ty — FE dùng cho chip filter companyId.</summary>
    public Guid CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogo { get; set; }

    /// <summary>Mô tả bộ câu hỏi (question_sets.HrNote) — null nếu HR không nhập.</summary>
    public string? Description { get; set; }

    public string Difficulty { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public int TotalQuestions { get; set; }

    /// <summary>Thời gian làm bài hiển thị: HR có đặt giới hạn thì lấy giới hạn đó, chưa đặt thì ước tính theo số câu.</summary>
    public int EstimatedTimeMinutes { get; set; }

    /// <summary>Giới hạn thời gian làm bài HR đặt (phút) — null = không giới hạn, hết giờ hệ thống tự nộp bài.</summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>Điểm trung bình các phiên luyện tập đã chấm (1 chữ số thập phân) — null khi chưa có phiên nào được chấm.</summary>
    public double? Rating { get; set; }

    /// <summary>Số lượt luyện tập trên bộ này.</summary>
    public int AttemptCount { get; set; }

    public List<CandidateQuestionItemDto> Questions { get; set; } = new();
}

/// <summary>1 câu hỏi hiển thị cho candidate — cố tình không có SampleAnswer/EvaluationCriteria.</summary>
public class CandidateQuestionItemDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string Question { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Skill { get; set; }
    public string? FocusArea { get; set; }
    public string? Rationale { get; set; }
    public List<object> Citations { get; set; } = new();
}

public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
