using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace WebAPI.Hubs;

/// <summary>
/// Hub realtime trạng thái thanh toán nâng Premium (SCRUM SignalR).
/// Client kết nối với JWT (access_token query) và tự vào group user:{userId}.
/// </summary>
[Authorize]
public class SubscriptionPaymentHub : Hub
{
    public const string HubPath = "/hubs/subscription-payments";
    public const string PaymentPaidEvent = "PaymentPaid";

    public static string UserGroup(Guid userId) => $"user:{userId:D}";

    public override async Task OnConnectedAsync()
    {
        var userId = ResolveUserId(Context.User);
        if (userId.HasValue)
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));

        await base.OnConnectedAsync();
    }

    private static Guid? ResolveUserId(ClaimsPrincipal? user)
    {
        var raw = user?.FindFirst("sub")?.Value
                  ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
