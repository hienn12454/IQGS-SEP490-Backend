namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Báo HR khi candidate chấp nhận lời mời/offer.
/// Dùng chung cho accept in-app và accept qua link email (SCRUM — HR không thấy gì).
/// </summary>
public interface IHrAcceptanceNotifier
{
    /// <summary>Best-effort: không throw ra ngoài — accept của candidate không được fail vì SMTP.</summary>
    Task NotifyAsync(Guid hrUserId, Guid candidateUserId, string? sharedPhoneNumber);
}
