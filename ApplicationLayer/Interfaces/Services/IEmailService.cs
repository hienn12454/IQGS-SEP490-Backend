namespace ApplicationLayer.Interfaces.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);
    Task SendEmailVerificationAsync(string toEmail, string toName, string otp);

    /// <summary>Gửi lời đề nghị phỏng vấn (offline) cho candidate — nội dung tự do HR nhập, kèm nút xem/xác nhận.</summary>
    Task SendCandidateOfferEmailAsync(string toEmail, string toName, string hrCompanyName, string offerMessage, string acceptLink);

    /// <summary>Báo cho HR biết candidate đã chấp nhận lời đề nghị — kèm thông tin candidate.</summary>
    Task SendCandidateOfferAcceptedNotificationAsync(
        string hrEmail, string hrName,
        string candidateName, string candidateEmail,
        string? targetRole, string? seniorityLevel, IReadOnlyList<string> techStack, string? phoneNumber,
        string appLink);
}
