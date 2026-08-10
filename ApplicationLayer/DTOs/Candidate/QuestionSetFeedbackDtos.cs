namespace ApplicationLayer.DTOs.Candidate;

/// <summary>Candidate gửi/sửa feedback (rating + nhận xét) cho 1 Question Set — chỉ khi đã có phiên luyện tập COMPLETED trên bộ này.</summary>
public class SubmitQuestionSetFeedbackDto
{
    /// <summary>1-5 sao, bắt buộc.</summary>
    public int Rating { get; set; }

    /// <summary>Nhận xét — tùy chọn, tối đa 2000 ký tự.</summary>
    public string? Comment { get; set; }
}

/// <summary>Query phân trang danh sách feedback của 1 question set.</summary>
public class QuestionSetFeedbackQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>1 feedback hiển thị công khai (marketplace detail) hoặc cho HR chủ sở hữu xem.</summary>
public class QuestionSetFeedbackDto
{
    public Guid Id { get; set; }
    public Guid CandidateUserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string? CandidateAvatarUrl { get; set; }

    /// <summary>Vị trí mục tiêu của candidate (CandidateProfile.TargetRole) — null nếu chưa khai báo.</summary>
    public string? CandidateTargetRole { get; set; }

    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Rating trung bình + danh sách feedback phân trang của 1 question set.</summary>
public class QuestionSetFeedbackSummaryDto
{
    public Guid QuestionSetId { get; set; }

    /// <summary>Rating trung bình (1-5) — null nếu chưa có feedback nào.</summary>
    public double? AverageRating { get; set; }

    public int TotalCount { get; set; }
    public List<QuestionSetFeedbackDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
}
