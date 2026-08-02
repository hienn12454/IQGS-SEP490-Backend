namespace DomainLayer.Entities;

/// <summary>Log giao dịch subscription (sandbox hoặc provider thật như SePay).</summary>
public class SubscriptionTransaction : BaseEntity
{
    public Guid SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = null!;

    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = "Paid";
    public string Provider { get; set; } = "Sandbox";
    public string? OrderCode { get; set; }
    public string? ExternalTransactionId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? RawPayloadJson { get; set; }
    public string? Note { get; set; }
}
