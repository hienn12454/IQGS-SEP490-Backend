using ApplicationLayer.DTOs.Subscription;
using ApplicationLayer.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;
using WebAPI.Hubs;

namespace WebAPI.Realtime;

/// <summary>Đẩy PaymentPaid / SubscriptionChanged qua SignalR tới đúng user.</summary>
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

    public async Task NotifySubscriptionChangedAsync(
        Guid userId,
        string planCode,
        string action,
        CancellationToken ct = default)
    {
        var payload = new SubscriptionChangedEventDto
        {
            PlanCode = planCode ?? string.Empty,
            Action = action ?? string.Empty,
            ChangedAt = DateTime.UtcNow
        };

        try
        {
            await _hub.Clients
                .Group(SubscriptionPaymentHub.UserGroup(userId))
                .SendAsync(SubscriptionPaymentHub.SubscriptionChangedEvent, payload, ct);

            _logger.LogInformation(
                "SignalR SubscriptionChanged → user={UserId} plan={PlanCode} action={Action}",
                userId, planCode, action);
        }
        catch (Exception ex)
        {
            // Không fail Admin API — FE còn poll 30s fallback.
            _logger.LogWarning(ex,
                "SignalR SubscriptionChanged thất bại user={UserId} plan={PlanCode} action={Action}",
                userId, planCode, action);
        }
    }
}
