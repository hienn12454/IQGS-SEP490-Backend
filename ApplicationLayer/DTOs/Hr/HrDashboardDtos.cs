namespace ApplicationLayer.DTOs.Hr;

/// <summary>SCRUM-339: query params cho GET /api/hr/dashboard.</summary>
public class HrDashboardQueryDto
{
    /// <summary>Số ngày hiển thị trên biểu đồ dailyActivity — mặc định 30, tối đa 365.</summary>
    public int ActivityDays { get; set; } = 30;

    /// <summary>Số phiên gần đây trả về trong recentSessions — mặc định 7, tối đa 50.</summary>
    public int RecentLimit { get; set; } = 7;

    /// <summary>Số candidate nổi bật trả về trong topRecommendations — mặc định 8, tối đa 50.</summary>
    public int RecommendationsLimit { get; set; } = 8;
}

/// <summary>SCRUM-339: response tổng hợp cho /hr/dashboard — gộp nhiều nguồn dữ liệu để FE không phải tự aggregate.</summary>
public class HrDashboardResponseDto
{
    public HrDashboardKpisDto Kpis { get; set; } = new();
    public List<HrDashboardDailyActivityItemDto> DailyActivity { get; set; } = new();
    public List<HrDashboardQuestionTypeDistributionItemDto> QuestionTypeDistribution { get; set; } = new();
    public List<HrDashboardRecentSessionDto> RecentSessions { get; set; } = new();
    public HrDashboardInsightsDto Insights { get; set; } = new();
    public List<HrDashboardTopRecommendationDto> TopRecommendations { get; set; } = new();
    public HrDashboardSubscriptionDto Subscription { get; set; } = new();
    public HrDashboardHiringFunnelDto HiringFunnel { get; set; } = new();
}

public class HrDashboardKpisDto
{
    /// <summary>Tổng số job sinh câu hỏi (từ JD) HR này đã tạo, mọi trạng thái.</summary>
    public int TotalSessions { get; set; }

    /// <summary>Số job đã hoàn thành (status COMPLETED).</summary>
    public int CompletedSessions { get; set; }

    /// <summary>Tổng số câu hỏi đã sinh ra từ tất cả job của HR này.</summary>
    public int TotalQuestionsGenerated { get; set; }

    /// <summary>CompletedSessions / TotalSessions * 100, làm tròn 2 chữ số thập phân — 0 nếu chưa có job nào.</summary>
    public double SuccessRatePercent { get; set; }

    /// <summary>Số job tạo trong tháng hiện tại (theo giờ UTC).</summary>
    public int ThisMonthSessions { get; set; }

    /// <summary>Role/chức danh xuất hiện nhiều nhất trong các job đã có plan — null nếu chưa có job nào có plan.</summary>
    public string? TopRole { get; set; }
}

public class HrDashboardDailyActivityItemDto
{
    /// <summary>Định dạng yyyy-MM-dd.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>Số job được tạo trong ngày này.</summary>
    public int Count { get; set; }
}

public class HrDashboardQuestionTypeDistributionItemDto
{
    /// <summary>1 trong 5 loại chuẩn hóa: technical, behavioral, situational, system-design, problem-solving.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Số câu hỏi thuộc loại này trên tất cả job của HR.</summary>
    public int Count { get; set; }
}

public class HrDashboardRecentSessionDto
{
    public Guid Id { get; set; }

    /// <summary>Đọc từ Plan.PlanJson — null nếu job chưa có plan (còn đang PLAN_QUEUED/PLAN_PROCESSING/FAILED trước khi có plan).</summary>
    public string? RoleTitle { get; set; }

    /// <summary>
    /// Trạng thái đã rút gọn từ 8 giá trị thực của QuestionGenerationJob.Status: COMPLETED giữ nguyên;
    /// FAILED và CANCELLED gộp thành FAILED; mọi trạng thái đang chạy/chờ khác
    /// (PLAN_QUEUED/PLAN_PROCESSING/WAITING_HR_APPROVAL/QUESTION_QUEUED/QUESTION_PROCESSING) gộp thành PROCESSING.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Số câu hỏi đã sinh cho job này (0 nếu chưa hoàn thành).</summary>
    public int QuestionCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class HrDashboardInsightsDto
{
    public string? TopRole { get; set; }
    public double SuccessRatePercent { get; set; }
    public string? TopQuestionType { get; set; }

    /// <summary>So sánh số job tạo trong 7 ngày gần nhất với 7 ngày liền trước đó: up/down/flat.</summary>
    public string WeekOverWeekTrend { get; set; } = "flat";

    /// <summary>Chênh lệch tuyệt đối số job giữa 2 khoảng 7 ngày nói trên (có thể âm).</summary>
    public int WeekOverWeekDelta { get; set; }
}

public class HrDashboardTopRecommendationDto
{
    public Guid Id { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string? TargetRole { get; set; }

    /// <summary>Thang 0–100, giống CandidateRecommendation.OverallScore.</summary>
    public double Score { get; set; }

    /// <summary>NEW | SHORTLISTED | DISMISSED | INVITED.</summary>
    public string Status { get; set; } = string.Empty;
}

public class HrDashboardSubscriptionDto
{
    /// <summary>Free hoặc Premium — đọc từ Subscription thật (SCRUM-336).</summary>
    public string PlanId { get; set; } = "Free";
}

public class HrDashboardHiringFunnelDto
{
    public int PracticedLast7Days { get; set; }
    public int NewUnviewed { get; set; }
    public int Shortlisted { get; set; }
    public int InvitedPending { get; set; }
    public int InvitedAccepted { get; set; }
}
