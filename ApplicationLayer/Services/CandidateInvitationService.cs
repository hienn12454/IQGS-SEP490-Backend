using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

/// <summary>Candidate xem và phản hồi lời mời phỏng vấn (SCRUM-295 / SCRUM-415).</summary>
public class CandidateInvitationService : ICandidateInvitationService
{
    private readonly ICandidateInvitationRepository _invitationRepository;
    private readonly ICandidateOfferRepository _offerRepository;
    private readonly IHrAcceptanceNotifier _acceptanceNotifier;

    public CandidateInvitationService(
        ICandidateInvitationRepository invitationRepository,
        ICandidateOfferRepository offerRepository,
        IHrAcceptanceNotifier acceptanceNotifier)
    {
        _invitationRepository = invitationRepository;
        _offerRepository = offerRepository;
        _acceptanceNotifier = acceptanceNotifier;
    }

    public async Task<IReadOnlyList<CandidateInvitationListItemDto>> ListAsync(Guid candidateUserId)
    {
        var rows = await _invitationRepository.ListByCandidateAsync(candidateUserId);

        return rows.Select(r => new CandidateInvitationListItemDto
        {
            Id = r.Id,
            CompanyName = r.CompanyName,
            CompanyLogo = CompanyLogoResolver.Resolve(r.CompanyLogo, r.CompanyWebsite, r.CompanyName),
            QuestionSetTitle = PublishedQuestionSetMapper.ResolveTitle(r.QuestionSetTitle, r.CompanyName),
            Message = r.Message,
            Status = r.Status,
            InvitedAt = r.InvitedAt,
            RespondedAt = r.RespondedAt,
            ScheduledAtUtc = r.ScheduledAtUtc,
            TimeZoneId = r.TimeZoneId,
            MeetingMode = r.MeetingMode,
            MeetingLink = r.MeetingLink,
            Location = r.Location
        }).ToList();
    }

    public Task<InvitationActionResponseDto> AcceptAsync(Guid id, Guid candidateUserId, AcceptInvitationRequestDto? dto)
        => RespondAsync(id, candidateUserId, InvitationStatus.Accepted, dto?.ResponseMessage, dto?.PhoneNumber);

    public Task<InvitationActionResponseDto> RejectAsync(Guid id, Guid candidateUserId)
        => RespondAsync(id, candidateUserId, InvitationStatus.Rejected, null, null);

    private async Task<InvitationActionResponseDto> RespondAsync(
        Guid id, Guid candidateUserId, string newStatus, string? responseMessage, string? sharedPhoneNumber)
    {
        var invitation = await _invitationRepository.GetByIdAsync(id)
            ?? throw new NotFoundException("Lời mời không tồn tại.");

        if (invitation.CandidateUserId != candidateUserId)
            throw new ForbiddenException("Bạn không có quyền phản hồi lời mời này.");

        if (invitation.Status != InvitationStatus.Pending)
            throw new ConflictException("Lời mời đã được phản hồi trước đó.");

        invitation.Status = newStatus;
        invitation.RespondedAt = DateTime.UtcNow;
        invitation.UpdatedAt = DateTime.UtcNow;

        // Chỉ lưu lời nhắn/SĐT khi ACCEPTED — candidate từ chối thì không cần chia sẻ gì thêm.
        if (newStatus == InvitationStatus.Accepted)
        {
            invitation.ResponseMessage = string.IsNullOrWhiteSpace(responseMessage) ? null : responseMessage.Trim();
            invitation.SharedPhoneNumber = string.IsNullOrWhiteSpace(sharedPhoneNumber) ? null : sharedPhoneNumber.Trim();
        }

        await _invitationRepository.UpdateAsync(invitation);

        // Accept in-app không đi qua CandidateOfferService — nếu HR đã gửi email offer
        // cùng recommendation, LatestOfferStatus vẫn SENT nên UI HR tưởng chưa accept.
        if (newStatus == InvitationStatus.Accepted)
        {
            await SyncOfferAcceptedAsync(invitation);
            // SCRUM-415: trước đây chỉ link email mới mail HR — accept trong app thì HR không nhận gì.
            await _acceptanceNotifier.NotifyAsync(
                invitation.HrUserId, invitation.CandidateUserId, invitation.SharedPhoneNumber);
        }

        return new InvitationActionResponseDto
        {
            Id = invitation.Id,
            Status = invitation.Status,
            RespondedAt = invitation.RespondedAt
        };
    }

    /// <summary>
    /// Đồng bộ offer SENT cùng recommendation thành ACCEPTED.
    /// Rec.Status giữ INVITED — HR lọc tab "Đã mời", trạng thái accept nằm ở InvitationStatus / LatestOfferStatus.
    /// </summary>
    private async Task SyncOfferAcceptedAsync(CandidateInvitation invitation)
    {
        var offer = await _offerRepository.GetLatestByRecommendationIdAsync(invitation.RecommendationId);
        if (offer is null || offer.Status != CandidateOfferStatus.Sent)
            return;

        offer.Status = CandidateOfferStatus.Accepted;
        offer.AcceptedAt = invitation.RespondedAt ?? DateTime.UtcNow;
        await _offerRepository.UpdateAsync(offer);
    }
}
