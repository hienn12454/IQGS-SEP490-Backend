using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services;

/// <summary>
/// Gửi email “candidate đã chấp nhận/từ chối” tới HR.
/// Tách riêng để InvitationService (in-app) và OfferService (link email) gọi cùng 1 chỗ.
/// </summary>
public class HrAcceptanceNotifier : IHrAcceptanceNotifier
{
    private readonly IUserRepository _userRepository;
    private readonly ICandidateProfileRepository _candidateProfileRepository;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<HrAcceptanceNotifier> _logger;

    public HrAcceptanceNotifier(
        IUserRepository userRepository,
        ICandidateProfileRepository candidateProfileRepository,
        IEmailService emailService,
        IConfiguration config,
        ILogger<HrAcceptanceNotifier> logger)
    {
        _userRepository = userRepository;
        _candidateProfileRepository = candidateProfileRepository;
        _emailService = emailService;
        _config = config;
        _logger = logger;
    }

    public async Task NotifyAsync(Guid hrUserId, Guid candidateUserId, string? sharedPhoneNumber, bool isAccepted = true)
    {
        try
        {
            var hrUser = await _userRepository.GetByIdAsync(hrUserId);
            var candidateUser = await _userRepository.GetByIdAsync(candidateUserId);
            if (hrUser is null || candidateUser is null)
            {
                _logger.LogWarning(
                    "Bỏ qua email báo phản hồi: không tìm thấy HR ({HrUserId}) hoặc candidate ({CandidateUserId}).",
                    hrUserId, candidateUserId);
                return;
            }

            var candidateProfile = await _candidateProfileRepository.GetByUserIdAsync(candidateUserId);
            var phone = !string.IsNullOrWhiteSpace(sharedPhoneNumber)
                ? sharedPhoneNumber.Trim()
                : candidateProfile?.PhoneNumber;
            var frontendUrl = (_config["AppSettings:FrontendUrl"] ?? "https://iqgs.com").TrimEnd('/');
            var appLink = $"{frontendUrl}/hr/candidate-recommendations";

            if (isAccepted)
            {
                await _emailService.SendCandidateOfferAcceptedNotificationAsync(
                    hrUser.Email, hrUser.FullName,
                    candidateUser.FullName, candidateUser.Email,
                    candidateProfile?.TargetRole, candidateProfile?.SeniorityLevel,
                    candidateProfile?.TechStack ?? Array.Empty<string>(),
                    phone,
                    appLink);
            }
            else
            {
                // SCRUM-482: reject trước đây im lặng — HR không biết candidate từ chối.
                await _emailService.SendCandidateOfferRejectedNotificationAsync(
                    hrUser.Email, hrUser.FullName,
                    candidateUser.FullName, candidateUser.Email,
                    candidateProfile?.TargetRole, candidateProfile?.SeniorityLevel,
                    candidateProfile?.TechStack ?? Array.Empty<string>(),
                    appLink);
            }
        }
        catch (Exception ex)
        {
            // Phản hồi đã commit — không rollback vì mail fail.
            _logger.LogError(
                ex,
                "Gửi email báo HR candidate {Outcome} thất bại. HrUserId={HrUserId}",
                isAccepted ? "chấp nhận" : "từ chối",
                hrUserId);
        }
    }
}
