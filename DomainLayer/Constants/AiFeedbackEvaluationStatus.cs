namespace DomainLayer.Constants;

/// <summary>Trạng thái đánh giá AI cho 1 câu trả lời (SCRUM-282 / SCRUM-332).</summary>
public static class AiFeedbackEvaluationStatus
{
    /// <summary>Đã lưu answer, chưa chấm AI (chờ complete).</summary>
    public const string Pending = "Pending";

    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

/// <summary>Mức truy cập AI feedback trên result (Teaser Freemium — SCRUM-332).</summary>
public static class PracticeFeedbackAccessLevel
{
    /// <summary>Free: overall teaser + 1 câu mẫu; các câu còn lại locked.</summary>
    public const string FreeTeaser = "FreeTeaser";

    /// <summary>Premium: full per-question + insight.</summary>
    public const string Full = "Full";
}
