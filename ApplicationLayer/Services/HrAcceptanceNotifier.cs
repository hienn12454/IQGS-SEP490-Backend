using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services;

/// <summary>
/// Gửi email “candidate đã chấp nhận” tới HR.
/// Tách riêng để cả InvitationService (in-app) và OfferService (link email) gọi cùng 1 chỗ.
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

    public async Task NotifyAsync(Guid hrUserId, Guid candidateUserId, string? sharedPhoneNumber)
    {
        try
        {
            var hrUser = await _userRepository.GetByIdAsync(hrUserId);
            var candidateUser = await _userRepository.GetByIdAsync(candidateUserId);
            if (hrUser is null || candidateUser is null)
            {
                _logger.LogWarning(
                    "Bỏ qua email báo accept: không tìm thấy HR ({HrUserId}) hoặc candidate ({CandidateUserId}).",
                    hrUserId, candidateUserId);
                return;
            }

            var candidateProfile = await _candidateProfileRepository.GetByUserIdAsync(candidateUserId);
            var phone = !string.IsNullOrWhiteSpace(sharedPhoneNumber)
                ? sharedPhoneNumber.Trim()
                : candidateProfile?.PhoneNumber;
            var frontendUrl = (_config["AppSettings:FrontendUrl"] ?? "https://iqgs.com").TrimEnd('/');

            await _emailService.SendCandidateOfferAcceptedNotificationAsync(
                hrUser.Email, hrUser.FullName,
                candidateUser.FullName, candidateUser.Email,
                candidateProfile?.TargetRole, candidateProfile?.SeniorityLevel,
                candidateProfile?.TechStack ?? Array.Empty<string>(),
                phone,
                $"{frontendUrl}/hr/candidate-recommendations");
        }
        catch (Exception ex)
        {
            // Accept đã commit — không rollback vì mail fail.
            _logger.LogError(ex, "Gửi email báo HR candidate chấp nhận thất bại. HrUserId={HrUserId}", hrUserId);
        }
    }
}
