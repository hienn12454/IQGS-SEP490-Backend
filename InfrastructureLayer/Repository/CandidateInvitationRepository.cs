using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class CandidateInvitationRepository : ICandidateInvitationRepository
{
    private readonly Database.AppDbContext _context;

    public CandidateInvitationRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<CandidateInvitation?> GetByIdAsync(Guid id)
        => _context.CandidateInvitations.FirstOrDefaultAsync(i => i.Id == id && i.IsActive);

    public Task<CandidateInvitation?> GetByRecommendationIdAsync(Guid recommendationId)
        => _context.CandidateInvitations.FirstOrDefaultAsync(i =>
            i.RecommendationId == recommendationId && i.IsActive);

    public async Task AddAsync(CandidateInvitation invitation)
    {
        await _context.CandidateInvitations.AddAsync(invitation);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CandidateInvitation invitation)
    {
        _context.CandidateInvitations.Update(invitation);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CandidateInvitationRow>> ListByCandidateAsync(Guid candidateUserId)
        => await _context.CandidateInvitations
            .AsNoTracking()
            .Where(i => i.CandidateUserId == candidateUserId && i.IsActive)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new CandidateInvitationRow
            {
                Id = i.Id,
                CompanyName = _context.HRProfiles
                    .Where(h => h.UserId == i.HrUserId)
                    .Select(h => h.Company.Name)
                    .FirstOrDefault() ?? string.Empty,
                CompanyLogo = _context.HRProfiles
                    .Where(h => h.UserId == i.HrUserId)
                    .Select(h => h.Company.LogoUrl)
                    .FirstOrDefault(),
                CompanyWebsite = _context.HRProfiles
                    .Where(h => h.UserId == i.HrUserId)
                    .Select(h => h.Company.WebsiteUrl)
                    .FirstOrDefault(),
                QuestionSetTitle = i.Recommendation.QuestionSet.Title,
                Message = i.Message,
                Status = i.Status,
                InvitedAt = i.CreatedAt,
                RespondedAt = i.RespondedAt,
                ScheduledAtUtc = i.ScheduledAtUtc,
                TimeZoneId = i.TimeZoneId,
                MeetingMode = i.MeetingMode,
                MeetingLink = i.MeetingLink,
                Location = i.Location
            })
            .ToListAsync();

    public async Task<(int Pending, int Accepted)> CountStatusByHrAsync(Guid hrUserId)
    {
        var pending = await _context.CandidateInvitations.CountAsync(i =>
            i.HrUserId == hrUserId && i.IsActive && i.Status == InvitationStatus.Pending);
        var accepted = await _context.CandidateInvitations.CountAsync(i =>
            i.HrUserId == hrUserId && i.IsActive && i.Status == InvitationStatus.Accepted);
        return (pending, accepted);
    }
}
