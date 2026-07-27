namespace DomainLayer.Entities;

/// <summary>
/// Đếm usage theo Subscription + PeriodStart + UsageType (+ ScopeKey cho regenerate theo draft).
/// </summary>
public class UsageCounter : BaseEntity
{
    public Guid SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = null!;

    /// <summary>Khớp Subscription.CurrentPeriodStart của kỳ đang đếm.</summary>
    public DateTime PeriodStart { get; set; }

    public string UsageType { get; set; } = string.Empty;

    /// <summary>Rỗng = counter tổng; với HrPlanRegenerate = draftId.</summary>
    public string ScopeKey { get; set; } = string.Empty;

    public int UsedCount { get; set; }

    /// <summary>Quota thêm từ pack (Ask-AI) trong kỳ — không mang sang kỳ sau.</summary>
    public int ExtraFromPack { get; set; }
}
