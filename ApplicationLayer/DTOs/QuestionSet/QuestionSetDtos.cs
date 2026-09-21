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

    /// <summary>PastedText | UploadedFile</summary>
    public string JdSourceType { get; set; } = "PastedText";

    /// <summary>Tên file gốc khi JD upload; null khi paste.</summary>
    public string? JdOriginalFileName { get; set; }

    /// <summary>SCRUM-465: path blob JD gốc (HR nội bộ).</summary>
    public string? JdBlobPath { get; set; }

    /// <summary>SCRUM-465: SAS ngắn hạn để HR preview file JD — null nếu không có blob.</summary>
    public string? JdFileUrl { get; set; }

    /// <summary>SCRUM-465: bản JD ngắn hiện cho candidate khi Tuyển.</summary>
    public string? PublicJobDescription { get; set; }

    /// <summary>SCRUM-468: địa điểm tin tuyển.</summary>
    public string? JobLocation { get; set; }

    /// <summary>SCRUM-468: AtOffice | Hybrid | Remote.</summary>
    public string? WorkplaceType { get; set; }

    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }
    public bool SalaryNegotiable { get; set; } = true;
    public string? JobExpertise { get; set; }
    public string? JobDomain { get; set; }

    public string? HrNote { get; set; }

    /// <summary>Giới hạn thời gian làm bài practice (phút) — null = không giới hạn.</summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>SCRUM-424: HR bật/tắt auto tạo recommendation khi candidate hoàn thành practice.</summary>
    public bool AutoRecommendEnabled { get; set; } = true;

    /// <summary>SCRUM-424: Ngưỡng OverallScore tối thiểu để tạo recommendation.</summary>
    public double RecommendationMinScore { get; set; } = 70;

    /// <summary>SCRUM-464: bộ Tuyển (JD public + official test).</summary>
    public bool IsHiringAssessment { get; set; }

    /// <summary>SCRUM-464: HR muốn anti-cheat trên bộ Tuyển.</summary>
    public bool HrAntiCheatEnabled { get; set; }

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
    /// <summary>SCRUM-408: số phiên IN_PROGRESS bị chuyển ABANDONED khi unpublish.</summary>
    public int AbandonedSessionCount { get; set; }
}

/// <summary>SCRUM-439: Publish selective + time limit (+ recommend settings) trong 1 request.</summary>
public class PublishQuestionSetRequestDto
{
    /// <summary>Null/empty = giữ active hiện tại; có list = chỉ các id này active, còn lại soft-deactivate.</summary>
    public List<Guid>? QuestionIds { get; set; }

    /// <summary>Số phút 1–480; null = không giới hạn thời gian practice.</summary>
    [System.ComponentModel.DataAnnotations.Range(1, 480, ErrorMessage = "Giới hạn thời gian phải từ 1 đến 480 phút.")]
    public int? TimeLimitMinutes { get; set; }

    /// <summary>Null = giữ cấu hình hiện tại; có giá trị = ghi khi publish.</summary>
    public bool? AutoRecommendEnabled { get; set; }

    /// <summary>Ngưỡng điểm gợi ý ứng viên 50–95; chỉ áp dụng khi AutoRecommendEnabled có gửi hoặc đang bật.</summary>
    [System.ComponentModel.DataAnnotations.Range(50, 95, ErrorMessage = "Ngưỡng điểm phải từ 50 đến 95.")]
    public double? RecommendationMinScore { get; set; }

    /// <summary>SCRUM-464: null = giữ cấu hình hiện tại.</summary>
    public bool? IsHiringAssessment { get; set; }

    /// <summary>SCRUM-464: null = giữ; chỉ có ý nghĩa khi bộ Tuyển.</summary>
    public bool? HrAntiCheatEnabled { get; set; }
}

/// <summary>SCRUM-438: Tổng hợp set PUBLISHED của HR (practice + rating).</summary>
public class PublishedOverviewItemDto
{
    public Guid QuestionSetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public int QuestionCount { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public int AttemptCount { get; set; }
    public int CompletedCount { get; set; }
    public int InProgressCount { get; set; }
    public double? AverageScore { get; set; }
    public double? AverageRating { get; set; }
    public int FeedbackCount { get; set; }

    /// <summary>SCRUM-464</summary>
    public bool IsHiringAssessment { get; set; }
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

/// <summary>SCRUM-424: cấu hình auto gợi ý ứng viên trên 1 bộ câu hỏi.</summary>
public class SetRecommendationSettingsRequestDto
{
    public bool AutoRecommendEnabled { get; set; } = true;

    /// <summary>Ngưỡng điểm 50–95 (thang 0–100).</summary>
    [System.ComponentModel.DataAnnotations.Range(50, 95, ErrorMessage = "Ngưỡng điểm phải từ 50 đến 95.")]
    public double RecommendationMinScore { get; set; } = 70;
}

public class SetRecommendationSettingsResponseDto
{
    public Guid QuestionSetId { get; set; }
    public bool AutoRecommendEnabled { get; set; }
    public double RecommendationMinScore { get; set; }
}

/// <summary>SCRUM-464: Practice vs Tuyển + anti-cheat HR (cho phép sửa khi PUBLISHED — ảnh hưởng phiên mới).</summary>
public class SetHiringAssessmentRequestDto
{
    public bool IsHiringAssessment { get; set; }

    /// <summary>Chỉ có hiệu lực khi IsHiringAssessment; thực tế cần Admin platform ON.</summary>
    public bool HrAntiCheatEnabled { get; set; }
}

public class SetHiringAssessmentResponseDto
{
    public Guid QuestionSetId { get; set; }
    public bool IsHiringAssessment { get; set; }
    public bool HrAntiCheatEnabled { get; set; }
}

/// <summary>SCRUM-465: bản JD ngắn cho candidate (cho phép sửa khi PUBLISHED).</summary>
public class SetPublicJobDescriptionRequestDto
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "PublicJobDescription không được để trống.")]
    [System.ComponentModel.DataAnnotations.MinLength(1, ErrorMessage = "PublicJobDescription không được để trống.")]
    [System.ComponentModel.DataAnnotations.MaxLength(20000, ErrorMessage = "PublicJobDescription tối đa 20000 ký tự.")]
    public string PublicJobDescription { get; set; } = string.Empty;
}

public class SetPublicJobDescriptionResponseDto
{
    public Guid QuestionSetId { get; set; }
    public string PublicJobDescription { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
}

/// <summary>SCRUM-468: metadata tin tuyển (location / salary / expertise / domain).</summary>
public class SetHiringPostingRequestDto
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "JobLocation không được để trống.")]
    [System.ComponentModel.DataAnnotations.MaxLength(200, ErrorMessage = "JobLocation tối đa 200 ký tự.")]
    public string JobLocation { get; set; } = string.Empty;

    /// <summary>AtOffice | Hybrid | Remote — optional.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(20)]
    public string? WorkplaceType { get; set; }

    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }

    /// <summary>true = thỏa thuận (không bắt buộc min/max).</summary>
    public bool SalaryNegotiable { get; set; } = true;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "JobExpertise không được để trống.")]
    [System.ComponentModel.DataAnnotations.MaxLength(120, ErrorMessage = "JobExpertise tối đa 120 ký tự.")]
    public string JobExpertise { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "JobDomain không được để trống.")]
    [System.ComponentModel.DataAnnotations.MaxLength(120, ErrorMessage = "JobDomain tối đa 120 ký tự.")]
    public string JobDomain { get; set; } = string.Empty;
}

public class SetHiringPostingResponseDto
{
    public Guid QuestionSetId { get; set; }
    public string? JobLocation { get; set; }
    public string? WorkplaceType { get; set; }
    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }
    public bool SalaryNegotiable { get; set; }
    public string? JobExpertise { get; set; }
    public string? JobDomain { get; set; }
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

public class UpdateQuestionSetJobDescriptionRequestDto
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Job description không được để trống.")]
    public string JobDescription { get; set; } = string.Empty;
}

public class UpdateQuestionSetJobDescriptionResponseDto
{
    public Guid QuestionSetId { get; set; }
    public bool HasJobDescription { get; set; }
    public int CharacterCount { get; set; }
    public string JdSourceType { get; set; } = "PastedText";
    public string? JdOriginalFileName { get; set; }
}

/// <summary>Read-model 1 candidate đã practice 1 bộ câu hỏi — dùng nội bộ bởi repository (SCRUM-326).</summary>
public class QuestionSetPractitionerRow
{
    public Guid SessionId { get; set; }
    public Guid CandidateUserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public string? SeniorityLevel { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? OverallScore { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>SCRUM-464: phiên bài test chính thức trên bộ Tuyển.</summary>
    public bool IsOfficialTest { get; set; }
}

/// <summary>SCRUM-326: 1 candidate đã practice bộ câu hỏi của HR — khác Recommendation (không lọc theo ngưỡng điểm).</summary>
public class QuestionSetPractitionerDto
{
    public Guid SessionId { get; set; }
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

    /// <summary>SCRUM-464</summary>
    public bool IsOfficialTest { get; set; }
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

    /// <summary>Mô tả bộ câu hỏi hiển thị trên card — lấy từ question_sets.HrNote sau khi lọc marker nội bộ (STUDIO_SAVE / STUDIO_MIRROR).</summary>
    public string? Description { get; set; }

    /// <summary>SCRUM-464</summary>
    public bool IsHiringAssessment { get; set; }

    /// <summary>SCRUM-467: bản ngắn — dùng tạo PublicJobDescriptionPreview trên list.</summary>
    public string? PublicJobDescription { get; set; }

    /// <summary>SCRUM-468: metadata tin tuyển (null khi Practice / chưa nhập).</summary>
    public string? JobLocation { get; set; }
    public string? WorkplaceType { get; set; }
    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }
    public bool SalaryNegotiable { get; set; } = true;
    public string? JobExpertise { get; set; }
    public string? JobDomain { get; set; }

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

    /// <summary>SCRUM-464</summary>
    public bool IsHiringAssessment { get; set; }

    /// <summary>SCRUM-464: raw JD extract — không expose nguyên văn cho candidate.</summary>
    public string? JobDescription { get; set; }

    /// <summary>SCRUM-465: bản ngắn HR soạn cho candidate.</summary>
    public string? PublicJobDescription { get; set; }

    /// <summary>SCRUM-468: metadata tin tuyển.</summary>
    public string? JobLocation { get; set; }
    public string? WorkplaceType { get; set; }
    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }
    public bool SalaryNegotiable { get; set; } = true;
    public string? JobExpertise { get; set; }
    public string? JobDomain { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>SCRUM-465</summary>
    public string JdSourceType { get; set; } = "PastedText";
    public string? JdOriginalFileName { get; set; }
    public string? JdBlobPath { get; set; }

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
    /// <summary>RubricV1 document hoặc legacy criteria array — FE normalizeFromUnknown.</summary>
    public object EvaluationCriteria { get; set; } = new List<object>();
    public List<object> Citations { get; set; } = new();
    /// <summary>SCRUM-439: false = không đưa lên marketplace (vẫn giữ trong draft để tick lại).</summary>
    public bool IsActive { get; set; } = true;
}
