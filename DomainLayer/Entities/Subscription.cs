namespace DomainLayer.Entities;

/// <summary>
/// Subscription gắn User. Kỳ anniversary: CurrentPeriodStart → CurrentPeriodEnd.
/// LimitsSnapshotJson = limit hiệu lực trong kỳ đang chạy.
/// </summary>
public class Subscription : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;

    public string Status { get; set; } = DomainLayer.Constants.SubscriptionStatus.Active;

    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }

    /// <summary>JSON limits khóa cho kỳ hiện tại — gate đọc field này.</summary>
    public string LimitsSnapshotJson { get; set; } = "{}";

    /// <summary>Lần Free HR tạo bộ thành công gần nhất — dùng cooldown 24h.</summary>
    public DateTime? LastSuccessfulGenerateAt { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>SCRUM-407: user hủy gia hạn — vẫn Premium đến CurrentPeriodEnd, rồi hạ Free.</summary>
    public bool CancelAtPeriodEnd { get; set; }

    public ICollection<UsageCounter> UsageCounters { get; set; } = new List<UsageCounter>();
    public ICollection<SubscriptionTransaction> Transactions { get; set; } = new List<SubscriptionTransaction>();
}
