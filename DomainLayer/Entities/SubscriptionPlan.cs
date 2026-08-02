namespace DomainLayer.Entities;

/// <summary>Template gói Free/Premium — Admin sửa LimitsJson; subscriber active giữ LimitsSnapshot đến hết kỳ.</summary>
public class SubscriptionPlan : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal PriceMonthly { get; set; }
    public string Currency { get; set; } = "VND";

    /// <summary>JSON <see cref="SubscriptionPlanLimits"/> — template; không áp dụng ngay cho subscriber giữa kỳ.</summary>
    public string LimitsJson { get; set; } = "{}";

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
