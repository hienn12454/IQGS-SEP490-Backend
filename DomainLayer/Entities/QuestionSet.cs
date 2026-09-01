using DomainLayer.Studio;

namespace DomainLayer.Entities;

public class QuestionSet : BaseEntity
{
    public Guid OwnerId { get; set; }

    /// <summary>Legacy V1 job — nullable sau Studio Save; sẽ bỏ khi drop pipeline V1.</summary>
    public Guid? SourceJobId { get; set; }

    /// <summary>Studio project nguồn (Save từ generate-v2) — FK Restrict (thay SourceJobId).</summary>
    public Guid? SourceProjectId { get; set; }

    public Guid? SourcePlanId { get; set; }
    public Guid? SourceRunId { get; set; }

    public string Status { get; set; } = Constants.QuestionSetStatus.Draft;

    /// <summary>Marketplace = bộ HR publish; Personal = bộ Candidate sinh từ CV+JD, không lên marketplace.</summary>
    public string Kind { get; set; } = Constants.QuestionSetKind.Marketplace;
    public string? Title { get; set; }
    public string JobDescription { get; set; } = string.Empty;

    /// <summary>PastedText | UploadedFile — nguồn JD khi gắn vào bộ.</summary>
    public string JdSourceType { get; set; } = "PastedText";

    /// <summary>Tên file gốc nếu JD upload (PDF/DOCX/TXT); null khi paste.</summary>
    public string? JdOriginalFileName { get; set; }

    public string? HrNote { get; set; }
    public string PlanJson { get; set; } = "{}";
    public DateTime? GeneratedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>Giới hạn thời gian làm bài practice (phút) do HR đặt — null = không giới hạn.</summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>SCRUM-424: HR bật/tắt auto tạo recommendation khi candidate hoàn thành practice.</summary>
    public bool AutoRecommendEnabled { get; set; } = true;

    /// <summary>SCRUM-424: Ngưỡng OverallScore (0–100) tối thiểu để tạo recommendation — mặc định 70.</summary>
    public double RecommendationMinScore { get; set; } = 70;

    /// <summary>SCRUM-404: Admin ghim bộ lên đầu Marketplace Candidate.</summary>
    public bool IsPinned { get; set; }

    /// <summary>SCRUM-404: Thời điểm Admin ghim — null khi chưa/không còn pin.</summary>
    public DateTime? PinnedAt { get; set; }

    public QuestionGenerationJob? SourceJob { get; set; }
    public InterviewProject? SourceProject { get; set; }
    public InterviewPlan? SourcePlan { get; set; }
    public QuestionGenerationRun? SourceRun { get; set; }
    public ICollection<QuestionSetQuestion> Questions { get; set; } = new List<QuestionSetQuestion>();
    public QuestionSetJdFitReview? JdFitReview { get; set; }
}
