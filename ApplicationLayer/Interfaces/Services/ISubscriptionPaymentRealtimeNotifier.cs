namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Đẩy sự kiện subscription realtime tới FE (SignalR).
/// Webhook SePay / Admin grant vẫn là source of truth; notifier chỉ báo UI.
/// </summary>
public interface ISubscriptionPaymentRealtimeNotifier
{
    /// <summary>Gửi event PaymentPaid tới group user:{userId}.</summary>
    Task NotifyPaymentPaidAsync(
        Guid userId,
        string orderCode,
        decimal amount,
        string currency,
        CancellationToken ct = default);

    /// <summary>
    /// Gửi event SubscriptionChanged khi Admin gán / gia hạn / thu hồi Premium.
    /// FE đã listen tên event này trong use-subscription-realtime.
    /// </summary>
    Task NotifySubscriptionChangedAsync(
        Guid userId,
        string planCode,
        string action,
        CancellationToken ct = default);
}
