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

    /// <summary>ID generation session (job) nguồn — null nếu set từ Studio Save.</summary>
    public Guid? JobId { get; set; }

    /// <summary>Studio project nguồn (nếu Save từ generate-v2).</summary>
    public Guid? SourceProjectId { get; set; }

    public string? Title { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>Tên công ty của HR sở hữu bộ này — chính là công ty candidate sẽ thấy trên marketplace sau khi publish.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Luôn có giá trị: logo thật nếu công ty đã upload, không thì fallback theo domain website hoặc tự vẽ từ tên công ty.</summary>
    public string CompanyLogo { get; set; } = string.Empty;

    public DateTime SavedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>SCRUM-391: số câu hỏi active trong bộ.</summary>
    public int QuestionCount { get; set; }

    /// <summary>SCRUM-391: HR đã bookmark bộ này chưa.</summary>
    public bool IsBookmarked { get; set; }
}

public class SaveDraftResponseDto
{
    public Guid QuestionSetId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? SourceJobId { get; set; }
    public Guid? SourceProjectId { get; set; }
    public int QuestionCount { get; set; }
    public DateTime SavedAt { get; set; }

    /// <summary>Tên công ty của HR sở hữu bộ này — chính là công ty candidate sẽ thấy trên marketplace sau khi publish.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Luôn có giá trị: logo thật nếu công ty đã upload, không thì fallback theo domain website hoặc tự vẽ từ tên công ty.</summary>
    public string CompanyLogo { get; set; } = string.Empty;
}

public class QuestionSetDetailResponseDto
{
    public Guid QuestionSetId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? SourceJobId { get; set; }
    public Guid? SourceProjectId { get; set; }
    public string? Title { get; set; }

    /// <summary>Tên công ty của HR sở hữu bộ này — chính là công ty candidate sẽ thấy trên marketplace sau khi publish.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Luôn có giá trị: logo thật nếu công ty đã upload, không thì fallback theo domain website hoặc tự vẽ từ tên công ty.</summary>
    public string CompanyLogo { get; set; } = string.Empty;

    public string JobDescription { get; set; } = string.Empty;
    public string? HrNote { get; set; }

    /// <summary>Giới hạn thời gian làm bài practice (phút) — null = không giới hạn.</summary>
    public int? TimeLimitMinutes { get; set; }
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

/// <summary>Request đặt giới hạn thời gian làm bài practice cho 1 bộ câu hỏi.</summary>
public class SetTimeLimitRequestDto
{
    /// <summary>Số phút giới hạn (1–480) — truyền null để bỏ giới hạn.</summary>
    [System.ComponentModel.DataAnnotations.Range(1, 480, ErrorMessage = "Giới hạn thời gian phải từ 1 đến 480 phút.")]
    public int? TimeLimitMinutes { get; set; }
}

public class SetTimeLimitResponseDto
{
    public Guid QuestionSetId { get; set; }
    public int? TimeLimitMinutes { get; set; }
}

/// <summary>SCRUM-397: tạo bộ câu hỏi DRAFT rỗng từ Question Builder (không qua Studio).</summary>
public class CreateManualDraftQuestionSetRequestDto
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [System.ComponentModel.DataAnnotations.MinLength(1, ErrorMessage = "Tiêu đề không được để trống.")]
    [System.ComponentModel.DataAnnotations.MaxLength(500, ErrorMessage = "Tiêu đề không được vượt quá 500 ký tự.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Mô tả ngắn — lưu vào HrNote (entity chưa có cột Description riêng).</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(2000, ErrorMessage = "Mô tả không được vượt quá 2000 ký tự.")]
    public string? Description { get; set; }
}

/// <summary>SCRUM-330: request đổi tên bộ câu hỏi sau khi đã tạo.</summary>
public class RenameQuestionSetTitleRequestDto
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [System.ComponentModel.DataAnnotations.MinLength(1, ErrorMessage = "Tiêu đề không được để trống.")]
    [System.ComponentModel.DataAnnotations.MaxLength(500, ErrorMessage = "Tiêu đề không được vượt quá 500 ký tự.")]
    public string Title { get; set; } = string.Empty;
}

public class RenameQuestionSetTitleResponseDto
{
    public Guid QuestionSetId { get; set; }
    public string Title { get; set; } = string.Empty;
}

/// <summary>Read-model 1 candidate đã practice 1 bộ câu hỏi — dùng nội bộ bởi repository (SCRUM-326).</summary>
public class QuestionSetPractitionerRow
{
    public Guid CandidateUserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public string? SeniorityLevel { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? OverallScore { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>SCRUM-326: 1 candidate đã practice bộ câu hỏi của HR — khác Recommendation (không lọc theo ngưỡng điểm).</summary>
public class QuestionSetPractitionerDto
{
    public Guid CandidateUserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public string? SeniorityLevel { get; set; }

    /// <summary>IN_PROGRESS | COMPLETED | ABANDONED.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Thang 0–100 — null nếu phiên chưa hoàn thành.</summary>
    public double? OverallScore { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>Read-model cho 1 dòng marketplace (projection JOIN question_sets/HRProfiles/Companies), dùng nội bộ bởi repository.</summary>
public class PublishedQuestionSetRow
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogo { get; set; }
    public string? CompanyWebsite { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string SkillsJson { get; set; } = "[]";

    /// <summary>Skill thực tế gộp distinct từ các câu hỏi active của bộ — nguồn chính cho chip filter (SkillsJson của job chỉ là fallback).</summary>
    public List<string> QuestionSkills { get; set; } = new();
    public int TotalQuestions { get; set; }

    /// <summary>Giới hạn thời gian làm bài HR đặt (phút) — null = không giới hạn.</summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>Mô tả bộ câu hỏi hiển thị trên card — lấy từ question_sets.HrNote.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// OverallScore trung bình các phiên luyện tập đã chấm — RAW, thang 0-100 (SCRUM-304).
    /// Không phải rating sao hiển thị cho candidate — PublishedQuestionSetMapper.RoundRating quy đổi sang thang 0-5 trước khi trả ra API.
    /// Null khi chưa có phiên nào được chấm.
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>Số phiên luyện tập đã tạo trên bộ này.</summary>
    public int AttemptCount { get; set; }

    /// <summary>SCRUM-404: Admin đã ghim.</summary>
    public bool IsPinned { get; set; }

    public DateTime? PinnedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

/// <summary>Read-model chi tiết 1 bộ đã publish, dùng nội bộ bởi repository — KHÔNG chứa SampleAnswer/EvaluationCriteria.</summary>
public class PublishedQuestionSetDetail
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogo { get; set; }
    public string? CompanyWebsite { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string SkillsJson { get; set; } = "[]";
    public int? TimeLimitMinutes { get; set; }
    public string? Description { get; set; }

    /// <summary>OverallScore trung bình RAW, thang 0-100 — xem ghi chú ở PublishedQuestionSetRow.Rating.</summary>
    public double? Rating { get; set; }

    public int AttemptCount { get; set; }

    /// <summary>SCRUM-404: Admin đã ghim.</summary>
    public bool IsPinned { get; set; }

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

    /// <summary>SCRUM-399: path Blob ảnh đính kèm — map sang SAS URL ở service Candidate.</summary>
    public string? AttachedImageBlobPath { get; set; }

    /// <summary>SCRUM-400: Text | Code (có thể null trên bộ cũ trước khi backfill).</summary>
    public string? AnswerMethod { get; set; }
}

/// <summary>Rubric nội bộ cho RAG evaluate — không expose qua Candidate marketplace API (SCRUM-282).</summary>
public class QuestionEvaluationRubric
{
    public Guid Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Skill { get; set; }
    public string? SampleAnswer { get; set; }
    public string EvaluationCriteriaJson { get; set; } = "[]";
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
    /// <summary>SCRUM-396: SAS URL ảnh đính kèm (Azure Blob).</summary>
    public string? AttachedImageUrl { get; set; }
    /// <summary>SCRUM-400: Text | Code.</summary>
    public string AnswerMethod { get; set; } = "Text";
    public List<object> EvaluationCriteria { get; set; } = new();
    public List<object> Citations { get; set; } = new();
}
