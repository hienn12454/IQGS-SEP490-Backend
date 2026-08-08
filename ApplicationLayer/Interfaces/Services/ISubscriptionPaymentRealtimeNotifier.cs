namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Đẩy sự kiện thanh toán subscription realtime tới FE (SignalR).
/// Webhook SePay vẫn là source of truth; notifier chỉ báo UI.
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
}
