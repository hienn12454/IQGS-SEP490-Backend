using ApplicationLayer.DTOs.Candidate;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

/// <summary>Đọc/ghi candidate_invitations (SCRUM-295).</summary>
public interface ICandidateInvitationRepository
{
    Task<CandidateInvitation?> GetByIdAsync(Guid id);

    Task<CandidateInvitation?> GetByRecommendationIdAsync(Guid recommendationId);

    Task AddAsync(CandidateInvitation invitation);

    Task UpdateAsync(CandidateInvitation invitation);

    /// <summary>Danh sách lời mời của candidate kèm tên/logo công ty và tên bộ câu hỏi, mới nhất trước.</summary>
    Task<IReadOnlyList<CandidateInvitationRow>> ListByCandidateAsync(Guid candidateUserId);

    Task<(int Pending, int Accepted)> CountStatusByHrAsync(Guid hrUserId);
}
