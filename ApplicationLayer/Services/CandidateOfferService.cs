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

/// <summary>
/// Lời đề nghị phỏng vấn qua email (offer). Email vẫn gửi như cũ, nhưng nếu recommendation
/// chưa có CandidateInvitation thì tạo thêm lời mời in-app — candidate thấy trong
/// GET /api/candidate/invitations (tránh HR bấm Gửi Offer mà hộp thư candidate trống).
/// </summary>
public class CandidateOfferService : ICandidateOfferService
{
    private readonly ICandidateOfferRepository _offerRepository;
    private readonly ICandidateRecommendationRepository _recommendationRepository;
    private readonly ICandidateInvitationRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IHrCompanyInfoService _hrCompanyInfoService;
    private readonly IEmailService _emailService;
    private readonly IHrAcceptanceNotifier _acceptanceNotifier;
    private readonly IConfiguration _config;
    private readonly int _tokenExpirationDays;

    public CandidateOfferService(
        ICandidateOfferRepository offerRepository,
        ICandidateRecommendationRepository recommendationRepository,
        ICandidateInvitationRepository invitationRepository,
        IUserRepository userRepository,
        IHrCompanyInfoService hrCompanyInfoService,
        IEmailService emailService,
        IHrAcceptanceNotifier acceptanceNotifier,
        IConfiguration config)
    {
        _offerRepository = offerRepository;
        _recommendationRepository = recommendationRepository;
        _invitationRepository = invitationRepository;
        _userRepository = userRepository;
        _hrCompanyInfoService = hrCompanyInfoService;
        _emailService = emailService;
        _acceptanceNotifier = acceptanceNotifier;
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
        await EnsureInAppInvitationAsync(recommendation, hrUserId, offer.Message);

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
        await SyncInvitationAcceptedAsync(offer);

        await _acceptanceNotifier.NotifyAsync(offer.HrUserId, offer.CandidateUserId, null);

        return CandidateOfferPageHtml.BuildSuccessPage(offer.Message);
    }

    /// <summary>
    /// Tạo lời mời in-app nếu chưa có (unique 1 invitation / recommendation).
    /// Không đụng invitation đã tồn tại — offer vẫn gửi được khi đã mời trước đó.
    /// </summary>
    private async Task EnsureInAppInvitationAsync(
        CandidateRecommendation recommendation, Guid hrUserId, string? message)
    {
        if (await _invitationRepository.GetByRecommendationIdAsync(recommendation.Id) is not null)
            return;

        var trimmed = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        // Invitation.Message max 2000; offer cho phép 5000 — cắt để không vỡ insert.
        if (trimmed is { Length: > 2000 })
            trimmed = trimmed[..2000];

        await _invitationRepository.AddAsync(new CandidateInvitation
        {
            RecommendationId = recommendation.Id,
            HrUserId = hrUserId,
            CandidateUserId = recommendation.CandidateUserId,
            Message = trimmed,
            Status = InvitationStatus.Pending
        });

        if (recommendation.Status != CandidateRecommendationStatus.Invited)
        {
            recommendation.Status = CandidateRecommendationStatus.Invited;
            recommendation.UpdatedAt = DateTime.UtcNow;
            await _recommendationRepository.UpdateAsync(recommendation);
        }
    }

    /// <summary>Candidate accept qua link email → đồng bộ invitation PENDING thành ACCEPTED.</summary>
    private async Task SyncInvitationAcceptedAsync(CandidateOffer offer)
    {
        var invitation = await _invitationRepository.GetByRecommendationIdAsync(offer.RecommendationId);
        if (invitation is null || invitation.Status != InvitationStatus.Pending)
            return;

        invitation.Status = InvitationStatus.Accepted;
        invitation.RespondedAt = offer.AcceptedAt ?? DateTime.UtcNow;
        invitation.UpdatedAt = DateTime.UtcNow;
        await _invitationRepository.UpdateAsync(invitation);
    }

    private async Task<CandidateOffer?> GetActiveOfferByRawTokenAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        return await _offerRepository.GetByTokenHashAsync(ComputeSha256(rawToken.Trim()));
    }

    private static string ComputeSha256(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
}
