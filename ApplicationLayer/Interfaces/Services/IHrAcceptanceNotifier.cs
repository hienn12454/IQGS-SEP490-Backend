namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Báo HR khi candidate chấp nhận hoặc từ chối lời mời/offer (SCRUM-415 / SCRUM-482).
/// </summary>
public interface IHrAcceptanceNotifier
{
    /// <summary>Best-effort: không throw ra ngoài — phản hồi của candidate không được fail vì SMTP.</summary>
    /// <param name="hrUserId">Id tài khoản HR nhận email.</param>
    /// <param name="candidateUserId">Id candidate vừa phản hồi.</param>
    /// <param name="sharedPhoneNumber">SĐT candidate chia sẻ khi accept (reject thì null).</param>
    /// <param name="isAccepted">true = chấp nhận; false = từ chối.</param>
    Task NotifyAsync(Guid hrUserId, Guid candidateUserId, string? sharedPhoneNumber, bool isAccepted = true);
}
