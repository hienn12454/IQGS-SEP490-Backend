using ApplicationLayer.DTOs.Subscription;
using ApplicationLayer.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;
using WebAPI.Hubs;

namespace WebAPI.Realtime;

/// <summary>Đẩy PaymentPaid qua SignalR tới đúng user đã mở modal thanh toán.</summary>
public class SignalRSubscriptionPaymentNotifier : ISubscriptionPaymentRealtimeNotifier
{
    private readonly IHubContext<SubscriptionPaymentHub> _hub;
    private readonly ILogger<SignalRSubscriptionPaymentNotifier> _logger;

    public SignalRSubscriptionPaymentNotifier(
        IHubContext<SubscriptionPaymentHub> hub,
        ILogger<SignalRSubscriptionPaymentNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyPaymentPaidAsync(
        Guid userId,
        string orderCode,
        decimal amount,
        string currency,
        CancellationToken ct = default)
    {
        var payload = new SubscriptionPaymentPaidEventDto
        {
            OrderCode = orderCode,
            Status = "Paid",
            Amount = amount,
            Currency = string.IsNullOrWhiteSpace(currency) ? "VND" : currency,
            ConfirmedAt = DateTime.UtcNow
        };

        try
        {
            await _hub.Clients
                .Group(SubscriptionPaymentHub.UserGroup(userId))
                .SendAsync(SubscriptionPaymentHub.PaymentPaidEvent, payload, ct);

            _logger.LogInformation(
                "SignalR PaymentPaid → user={UserId} order={OrderCode}",
                userId, orderCode);
        }
        catch (Exception ex)
        {
            // Không fail webhook vì UI missed push — FE còn poll fallback.
            _logger.LogWarning(ex,
                "SignalR PaymentPaid thất bại user={UserId} order={OrderCode}",
                userId, orderCode);
        }
    }
}
