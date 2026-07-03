namespace ApplicationLayer.DTOs.Candidate;

public class StartPracticeSessionDto
{
    public Guid QuestionSetId { get; set; }
}

public class PracticeSessionResponseDto
{
    public Guid Id { get; set; }
    public Guid QuestionSetId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? OverallScore { get; set; }

    /// <summary>Giới hạn thời gian làm bài (phút) HR đặt — null = không giới hạn.</summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>Hạn chót nộp bài (StartedAt + TimeLimitMinutes) — FE dùng để đếm ngược; null nếu không giới hạn. Quá mốc này hệ thống tự nộp bài.</summary>
    public DateTime? ExpiresAt { get; set; }

    public List<PracticeSessionQuestionDto> Questions { get; set; } = new();
}

public class PracticeSessionQuestionDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string Question { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Skill { get; set; }
    public string? FocusArea { get; set; }

    /// <summary>Câu trả lời candidate đã submit cho câu hỏi này (null nếu chưa trả lời) — SCRUM-278.</summary>
    public string? AnswerText { get; set; }
}

public class PracticeSessionCompleteResponseDto
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public double? OverallScore { get; set; }
    public int? DurationSeconds { get; set; }
}

/// <summary>Query danh sách phiên luyện tập — hỗ trợ lọc theo status, questionSet, keyword, khoảng ngày (SCRUM-284/298).</summary>
public class PracticeSessionListQueryDto
{
    /// <summary>Bỏ trống = mặc định COMPLETED (AC-02 SCRUM-284). Truyền tường minh để lấy IN_PROGRESS/ABANDONED (SCRUM-298).</summary>
    public string? Status { get; set; }

    public Guid? QuestionSetId { get; set; }

    /// <summary>Tìm theo tên bộ câu hỏi hoặc tên công ty.</summary>
    public string? Keyword { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Lọc theo StartedAt — xem DateRangeFilterHelper để biết thứ tự ưu tiên.</summary>
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
}

public class PracticeSessionListItemDto
{
    public Guid SessionId { get; set; }
    public Guid QuestionSetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double? Score { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class PracticeSessionStatsQueryDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
}

public class PracticeSessionStatsDto
{
    public int TotalSessions { get; set; }
    public double? AverageScore { get; set; }
    public double? BestScore { get; set; }

    /// <summary>Điểm của phiên hoàn thành gần nhất có điểm (theo CompletedAt) — null khi chưa có phiên nào được chấm.</summary>
    public double? LatestScore { get; set; }

    public long TotalDurationSeconds { get; set; }
}

/// <summary>Read-model 1 dòng session (projection JOIN practice_sessions/question_sets/HRProfiles/Companies) — dùng nội bộ bởi repository.</summary>
public class PracticeSessionRow
{
    public Guid SessionId { get; set; }
    public Guid QuestionSetId { get; set; }
    public string? SetTitle { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double? Score { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
