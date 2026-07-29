using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

/// <summary>Đọc/ghi candidate_offers.</summary>
public interface ICandidateOfferRepository
{
    Task<CandidateOffer?> GetByIdAsync(Guid id);

    /// <summary>Lookup công khai cho luồng GET/POST accept theo hash của raw token.</summary>
    Task<CandidateOffer?> GetByTokenHashAsync(string tokenHash);

    /// <summary>Offer gần nhất (theo CreatedAt) của 1 recommendation — dùng cho guard "đã ACCEPTED thì chặn gửi lại".</summary>
    Task<CandidateOffer?> GetLatestByRecommendationIdAsync(Guid recommendationId);

    Task AddAsync(CandidateOffer offer);

    Task UpdateAsync(CandidateOffer offer);
}
