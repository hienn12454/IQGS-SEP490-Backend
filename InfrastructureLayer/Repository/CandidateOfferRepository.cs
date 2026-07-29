using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class CandidateOfferRepository : ICandidateOfferRepository
{
    private readonly Database.AppDbContext _context;

    public CandidateOfferRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<CandidateOffer?> GetByIdAsync(Guid id)
        => _context.CandidateOffers.FirstOrDefaultAsync(o => o.Id == id && o.IsActive);

    public Task<CandidateOffer?> GetByTokenHashAsync(string tokenHash)
        => _context.CandidateOffers.FirstOrDefaultAsync(o => o.TokenHash == tokenHash && o.IsActive);

    public Task<CandidateOffer?> GetLatestByRecommendationIdAsync(Guid recommendationId)
        => _context.CandidateOffers
            .Where(o => o.RecommendationId == recommendationId && o.IsActive)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task AddAsync(CandidateOffer offer)
    {
        await _context.CandidateOffers.AddAsync(offer);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CandidateOffer offer)
    {
        _context.CandidateOffers.Update(offer);
        await _context.SaveChangesAsync();
    }
}
