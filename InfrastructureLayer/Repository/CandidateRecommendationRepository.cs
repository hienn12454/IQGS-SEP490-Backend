using ApplicationLayer.DTOs.Recommendation;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class CandidateRecommendationRepository : ICandidateRecommendationRepository
{
    private readonly Database.AppDbContext _context;

    public CandidateRecommendationRepository(Database.AppDbContext context)
    {
        _context = context;
    }

    public Task<CandidateRecommendation?> GetByIdAsync(Guid id)
        => _context.CandidateRecommendations.FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

    public Task<CandidateRecommendation?> GetByCandidateAndSetAsync(Guid candidateUserId, Guid questionSetId)
        => _context.CandidateRecommendations.FirstOrDefaultAsync(r =>
            r.CandidateUserId == candidateUserId && r.QuestionSetId == questionSetId && r.IsActive);

    public async Task AddAsync(CandidateRecommendation recommendation)
    {
        await _context.CandidateRecommendations.AddAsync(recommendation);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CandidateRecommendation recommendation)
    {
        _context.CandidateRecommendations.Update(recommendation);
        await _context.SaveChangesAsync();
    }

    public Task<QuestionSet?> GetPublishedQuestionSetAsync(Guid questionSetId)
        => _context.QuestionSets
            .AsNoTracking()
            .FirstOrDefaultAsync(qs =>
                qs.Id == questionSetId && qs.Status == QuestionSetStatus.Published && qs.IsActive);

    public async Task<(IReadOnlyList<HrRecommendationRow> Items, int TotalCount)> ListByHrAsync(
        Guid hrOwnerId, int page, int pageSize, string? status, Guid? questionSetId)
    {
        var query = _context.CandidateRecommendations
            .AsNoTracking()
            .Where(r => r.HrOwnerId == hrOwnerId && r.IsActive);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (questionSetId.HasValue)
            query = query.Where(r => r.QuestionSetId == questionSetId.Value);

        var projected = query
            .Join(_context.Users.AsNoTracking(),
                r => r.CandidateUserId, u => u.Id,
                (r, u) => new { r, u })
            .Join(_context.CandidateProfiles.AsNoTracking(),
                x => x.r.CandidateUserId, p => p.UserId,
                (x, p) => new { x.r, x.u, p })
            // Candidate tắt AllowRecruiterRecommendation -> ẩn NGAY khỏi list HR xem được, kể cả recommendation
            // đã tạo từ trước (không chỉ chặn tạo mới) — HR không còn thấy tên/email/điểm cho đến khi candidate bật lại.
            .Where(x => x.p.AllowRecruiterRecommendation)
            .OrderByDescending(x => x.r.OverallScore)
            .ThenByDescending(x => x.r.CreatedAt)
            .Select(x => new HrRecommendationRow
            {
                Id = x.r.Id,
                CandidateUserId = x.r.CandidateUserId,
                CandidateName = x.u.FullName,
                CandidateEmail = x.u.Email,
                TargetRole = x.p.TargetRole,
                SeniorityLevel = x.p.SeniorityLevel,
                TechStack = x.p.TechStack,
                QuestionSetId = x.r.QuestionSetId,
                QuestionSetTitle = x.r.QuestionSet.Title,
                OverallScore = x.r.OverallScore,
                Status = x.r.Status,
                InvitationStatus = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.Status)
                    .FirstOrDefault(),
                InvitationResponseMessage = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.ResponseMessage)
                    .FirstOrDefault(),
                InvitationSharedPhoneNumber = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.SharedPhoneNumber)
                    .FirstOrDefault(),
                RecommendedAt = x.r.CreatedAt
            });

        var totalCount = await projected.CountAsync();
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
