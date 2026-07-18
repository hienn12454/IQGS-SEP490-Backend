using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.Interfaces.Services;

public interface ICandidateInvitationService
{
    Task<IReadOnlyList<CandidateInvitationListItemDto>> ListAsync(Guid candidateUserId);
    Task<InvitationActionResponseDto> AcceptAsync(Guid id, Guid candidateUserId, AcceptInvitationRequestDto? dto);
    Task<InvitationActionResponseDto> RejectAsync(Guid id, Guid candidateUserId);
}
