using System.Security.Cryptography;
using System.Text;
using ApplicationLayer.DTOs.Recommendation;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Configuration;

namespace ApplicationLayer.Services;

/// <summary>Lời đề nghị phỏng vấn qua email (offer) — độc lập với luồng CandidateInvitation in-app.</summary>
public class CandidateOfferService : ICandidateOfferService
{
    private readonly ICandidateOfferRepository _offerRepository;
    private readonly ICandidateRecommendationRepository _recommendationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICandidateProfileRepository _candidateProfileRepository;
    private readonly IHrCompanyInfoService _hrCompanyInfoService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly int _tokenExpirationDays;

    public CandidateOfferService(
        ICandidateOfferRepository offerRepository,
        ICandidateRecommendationRepository recommendationRepository,
        IUserRepository userRepository,
        ICandidateProfileRepository candidateProfileRepository,
        IHrCompanyInfoService hrCompanyInfoService,
        IEmailService emailService,
        IConfiguration config)
    {
        _offerRepository = offerRepository;
        _recommendationRepository = recommendationRepository;
        _userRepository = userRepository;
        _candidateProfileRepository = candidateProfileRepository;
        _hrCompanyInfoService = hrCompanyInfoService;
        _emailService = emailService;
        _config = config;
        _tokenExpirationDays = int.Parse(config["CandidateOfferSettings:TokenExpirationDays"] ?? "7");
    }

    public async Task<SendCandidateOfferResponseDto> SendOfferAsync(
        Guid recommendationId, Guid hrUserId, SendCandidateOfferRequestDto dto)
    {
        var recommendation = await _recommendationRepository.GetByIdAsync(recommendationId)
            ?? throw new NotFoundException("Recommendation không tồn tại.");
        if (recommendation.HrOwnerId != hrUserId)
            throw new ForbiddenException("Bạn không có quyền thao tác recommendation này.");

        var latestOffer = await _offerRepository.GetLatestByRecommendationIdAsync(recommendationId);
        if (latestOffer is not null)
        {
            if (latestOffer.Status == CandidateOfferStatus.Accepted)
                throw new ConflictException("Candidate đã chấp nhận lời đề nghị trước đó — không thể gửi thêm.");
            if (latestOffer.Status == CandidateOfferStatus.Sent && latestOffer.TokenExpiresAt >= DateTime.UtcNow)
                throw new ConflictException("Đã gửi offer còn hạn — không thể gửi thêm.");
        }

        var candidate = await _userRepository.GetByIdAsync(recommendation.CandidateUserId)
            ?? throw new NotFoundException("Không tìm thấy thông tin candidate.");

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var offer = new CandidateOffer
        {
            RecommendationId = recommendation.Id,
            HrUserId = hrUserId,
            CandidateUserId = recommendation.CandidateUserId,
            Message = dto.Message.Trim(),
            Status = CandidateOfferStatus.Sent,
            TokenHash = ComputeSha256(rawToken),
            TokenExpiresAt = DateTime.UtcNow.AddDays(_tokenExpirationDays)
        };
        await _offerRepository.AddAsync(offer);

        var apiBaseUrl = (_config["AppSettings:ApiBaseUrl"] ?? "https://localhost:5001").TrimEnd('/');
        var acceptLink = $"{apiBaseUrl}/api/public/candidate-offers/{rawToken}";
        var (companyName, _) = await _hrCompanyInfoService.GetByHrUserIdAsync(hrUserId);

        await _emailService.SendCandidateOfferEmailAsync(
            candidate.Email, candidate.FullName, companyName, offer.Message, acceptLink);

        return new SendCandidateOfferResponseDto
        {
            RecommendationId = recommendation.Id,
            OfferId = offer.Id,
            Status = offer.Status,
            SentAt = offer.CreatedAt
        };
    }

    public async Task<string> RenderConfirmPageAsync(string rawToken)
    {
        var offer = await GetActiveOfferByRawTokenAsync(rawToken)
            ?? throw new NotFoundException("Đường dẫn không hợp lệ.");

        if (offer.Status == CandidateOfferStatus.Accepted)
            return CandidateOfferPageHtml.BuildAlreadyAcceptedPage(offer.Message, offer.AcceptedAt);

        if (offer.TokenExpiresAt < DateTime.UtcNow)
            throw new BadRequestException("Đường dẫn đã hết hạn.");

        return CandidateOfferPageHtml.BuildConfirmPage(offer.Message, rawToken);
    }

    public async Task<string> CommitAcceptAsync(string rawToken)
    {
        var offer = await GetActiveOfferByRawTokenAsync(rawToken)
            ?? throw new NotFoundException("Đường dẫn không hợp lệ.");

        if (offer.Status == CandidateOfferStatus.Accepted)
            return CandidateOfferPageHtml.BuildSuccessPage(offer.Message);

        if (offer.TokenExpiresAt < DateTime.UtcNow)
            throw new BadRequestException("Đường dẫn đã hết hạn.");

        offer.Status = CandidateOfferStatus.Accepted;
        offer.AcceptedAt = DateTime.UtcNow;
        await _offerRepository.UpdateAsync(offer);

        await SendHrNotificationAsync(offer);

        return CandidateOfferPageHtml.BuildSuccessPage(offer.Message);
    }

    private async Task<CandidateOffer?> GetActiveOfferByRawTokenAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        return await _offerRepository.GetByTokenHashAsync(ComputeSha256(rawToken.Trim()));
    }

    private async Task SendHrNotificationAsync(CandidateOffer offer)
    {
        var hrUser = await _userRepository.GetByIdAsync(offer.HrUserId);
        var candidateUser = await _userRepository.GetByIdAsync(offer.CandidateUserId);
        if (hrUser is null || candidateUser is null) return; // best-effort, không chặn accept

        var candidateProfile = await _candidateProfileRepository.GetByUserIdAsync(offer.CandidateUserId);
        var frontendUrl = (_config["AppSettings:FrontendUrl"] ?? "https://iqgs.com").TrimEnd('/');

        await _emailService.SendCandidateOfferAcceptedNotificationAsync(
            hrUser.Email, hrUser.FullName,
            candidateUser.FullName, candidateUser.Email,
            candidateProfile?.TargetRole, candidateProfile?.SeniorityLevel,
            candidateProfile?.TechStack ?? Array.Empty<string>(),
            candidateProfile?.PhoneNumber,
            $"{frontendUrl}/hr/recommendations");
    }

    private static string ComputeSha256(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
}
