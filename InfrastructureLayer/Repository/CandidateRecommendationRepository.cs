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
        Guid hrOwnerId,
        int page,
        int pageSize,
        string? status,
        Guid? questionSetId,
        double? minScore = null,
        string sortBy = "score",
        string sortDir = "desc")
    {
        var baseQuery = BuildHrScopedQuery(hrOwnerId, status, questionSetId, minScore);
        var projected = ProjectRows(baseQuery);
        projected = ApplySort(projected, sortBy, sortDir);

        var totalCount = await projected.CountAsync();
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<HrRecommendationRow?> GetRowByIdForHrAsync(Guid id, Guid hrOwnerId)
    {
        var baseQuery = _context.CandidateRecommendations
            .AsNoTracking()
            .Where(r => r.Id == id && r.HrOwnerId == hrOwnerId && r.IsActive);

        return await ProjectRows(baseQuery).FirstOrDefaultAsync();
    }

    public async Task<HrRecommendationRow?> GetDetailRowByIdForHrAsync(Guid id, Guid hrOwnerId)
    {
        var baseQuery = _context.CandidateRecommendations
            .AsNoTracking()
            .Where(r => r.Id == id && r.HrOwnerId == hrOwnerId && r.IsActive);

        // Detail projection: thêm avatar + profile contact/social + CV meta (SCRUM-377).
        // List vẫn dùng ProjectRows mỏng để tránh phình payload.
        return await baseQuery
            .Join(_context.Users.AsNoTracking(),
                r => r.CandidateUserId, u => u.Id,
                (r, u) => new { r, u })
            .Join(_context.CandidateProfiles.AsNoTracking(),
                x => x.r.CandidateUserId, p => p.UserId,
                (x, p) => new { x.r, x.u, p })
            .Select(x => new HrRecommendationRow
            {
                Id = x.r.Id,
                CandidateUserId = x.r.CandidateUserId,
                CandidateName = x.u.FullName,
                CandidateEmail = x.u.Email,
                AvatarUrl = x.u.AvatarUrl,
                TargetRole = x.p.TargetRole,
                SeniorityLevel = x.p.SeniorityLevel,
                TechStack = x.p.TechStack,
                Bio = x.p.Bio,
                Address = x.p.Address,
                PhoneNumber = x.p.PhoneNumber,
                LinkedInUrl = x.p.LinkedInUrl,
                GithubUrl = x.p.GithubUrl,
                CvFileName = x.p.CvFileName,
                CvBlobPath = x.p.CvBlobPath,
                CvContentType = x.p.CvContentType,
                CvUploadedAt = x.p.CvUploadedAt,
                CvEvaluationJson = x.p.CvEvaluationJson,
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
            })
            .FirstOrDefaultAsync();
    }

    private IQueryable<CandidateRecommendation> BuildHrScopedQuery(
        Guid hrOwnerId, string? status, Guid? questionSetId, double? minScore)
    {
        var query = _context.CandidateRecommendations
            .AsNoTracking()
            .Where(r => r.HrOwnerId == hrOwnerId && r.IsActive);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (questionSetId.HasValue)
            query = query.Where(r => r.QuestionSetId == questionSetId.Value);

        if (minScore.HasValue)
            query = query.Where(r => r.OverallScore >= minScore.Value);

        return query;
    }

    private IQueryable<HrRecommendationRow> ProjectRows(IQueryable<CandidateRecommendation> query)
    {
        return query
            .Join(_context.Users.AsNoTracking(),
                r => r.CandidateUserId, u => u.Id,
                (r, u) => new { r, u })
            .Join(_context.CandidateProfiles.AsNoTracking(),
                x => x.r.CandidateUserId, p => p.UserId,
                (x, p) => new { x.r, x.u, p })
            // Candidate tắt AllowRecruiterRecommendation -> ẩn NGAY khỏi list HR xem được, kể cả recommendation
            // đã tạo từ trước (không chỉ chặn tạo mới) — HR không còn thấy tên/email/điểm cho đến khi candidate bật lại.
            .Where(x => x.p.AllowRecruiterRecommendation)
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
    }

    private static IQueryable<HrRecommendationRow> ApplySort(
        IQueryable<HrRecommendationRow> query, string sortBy, string sortDir)
    {
        var byDate = string.Equals(sortBy, "date", StringComparison.OrdinalIgnoreCase);
        var ascending = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        if (byDate)
        {
            return ascending
                ? query.OrderBy(x => x.RecommendedAt).ThenBy(x => x.OverallScore)
                : query.OrderByDescending(x => x.RecommendedAt).ThenByDescending(x => x.OverallScore);
        }

        // default: score
        return ascending
            ? query.OrderBy(x => x.OverallScore).ThenBy(x => x.RecommendedAt)
            : query.OrderByDescending(x => x.OverallScore).ThenByDescending(x => x.RecommendedAt);
    }
}
