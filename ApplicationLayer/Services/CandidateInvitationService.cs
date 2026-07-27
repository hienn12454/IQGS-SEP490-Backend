using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

/// <summary>Candidate xem và phản hồi lời mời phỏng vấn (SCRUM-295).</summary>
public class CandidateInvitationService : ICandidateInvitationService
{
    private readonly ICandidateInvitationRepository _invitationRepository;

    public CandidateInvitationService(ICandidateInvitationRepository invitationRepository)
    {
        _invitationRepository = invitationRepository;
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
            RespondedAt = r.RespondedAt
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

        return new InvitationActionResponseDto
        {
            Id = invitation.Id,
            Status = invitation.Status,
            RespondedAt = invitation.RespondedAt
        };
    }
}
