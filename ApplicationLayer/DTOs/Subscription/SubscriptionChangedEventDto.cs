namespace ApplicationLayer.DTOs.Subscription;

/// <summary>
/// Payload SignalR event SubscriptionChanged — Admin grant / extend / revoke Premium.
/// FE refresh /api/me/subscription để mở/khóa gen ngay (không chờ poll).
/// </summary>
public class SubscriptionChangedEventDto
{
    public string PlanCode { get; set; } = string.Empty;
    /// <summary>Granted | Extended | Revoked</summary>
    public string Action { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
