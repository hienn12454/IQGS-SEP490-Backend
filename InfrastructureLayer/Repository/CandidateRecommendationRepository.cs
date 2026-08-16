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
        string sortDir = "desc",
        bool unviewed = false)
    {
        var baseQuery = BuildHrScopedQuery(hrOwnerId, status, questionSetId, minScore, unviewed);
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
            // Candidate tắt AllowRecruiterRecommendation -> ẩn NGAY khỏi cả trang chi tiết/CV, không chỉ list
            // (trước đây thiếu filter này nên HR vẫn xem/tải CV được nếu đã có sẵn link recommendation id).
            .Where(x => x.p.AllowRecruiterRecommendation)
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
                PracticeSessionId = x.r.PracticeSessionId,
                ViewedAt = x.r.ViewedAt,
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
                InvitationScheduledAtUtc = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.ScheduledAtUtc)
                    .FirstOrDefault(),
                InvitationTimeZoneId = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.TimeZoneId)
                    .FirstOrDefault(),
                InvitationMeetingMode = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.MeetingMode)
                    .FirstOrDefault(),
                InvitationMeetingLink = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.MeetingLink)
                    .FirstOrDefault(),
                InvitationLocation = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.Location)
                    .FirstOrDefault(),
                RecommendedAt = x.r.CreatedAt,
                LatestOfferStatus = _context.CandidateOffers
                    .Where(o => o.RecommendationId == x.r.Id && o.IsActive)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => o.Status)
                    .FirstOrDefault(),
                OfferSentAt = _context.CandidateOffers
                    .Where(o => o.RecommendationId == x.r.Id && o.IsActive)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => (DateTime?)o.CreatedAt)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();
    }

    private IQueryable<CandidateRecommendation> BuildHrScopedQuery(
        Guid hrOwnerId, string? status, Guid? questionSetId, double? minScore, bool unviewed = false)
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

        if (unviewed)
            query = query.Where(r => r.ViewedAt == null && r.Status == CandidateRecommendationStatus.New);

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
                CvEvaluationJson = x.p.CvEvaluationJson,
                QuestionSetId = x.r.QuestionSetId,
                QuestionSetTitle = x.r.QuestionSet.Title,
                OverallScore = x.r.OverallScore,
                Status = x.r.Status,
                PracticeSessionId = x.r.PracticeSessionId,
                ViewedAt = x.r.ViewedAt,
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
                InvitationScheduledAtUtc = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.ScheduledAtUtc)
                    .FirstOrDefault(),
                InvitationTimeZoneId = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.TimeZoneId)
                    .FirstOrDefault(),
                InvitationMeetingMode = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.MeetingMode)
                    .FirstOrDefault(),
                InvitationMeetingLink = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.MeetingLink)
                    .FirstOrDefault(),
                InvitationLocation = _context.CandidateInvitations
                    .Where(i => i.RecommendationId == x.r.Id && i.IsActive)
                    .Select(i => i.Location)
                    .FirstOrDefault(),
                RecommendedAt = x.r.CreatedAt,
                LatestOfferStatus = _context.CandidateOffers
                    .Where(o => o.RecommendationId == x.r.Id && o.IsActive)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => o.Status)
                    .FirstOrDefault(),
                OfferSentAt = _context.CandidateOffers
                    .Where(o => o.RecommendationId == x.r.Id && o.IsActive)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => (DateTime?)o.CreatedAt)
                    .FirstOrDefault()
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

    public async Task<IReadOnlyList<HrRecommendationRow>> GetDetailRowsByIdsForHrAsync(
        IReadOnlyList<Guid> ids, Guid hrOwnerId)
    {
        if (ids.Count == 0)
            return Array.Empty<HrRecommendationRow>();

        var baseQuery = _context.CandidateRecommendations
            .AsNoTracking()
            .Where(r => ids.Contains(r.Id) && r.HrOwnerId == hrOwnerId && r.IsActive);

        return await ProjectRows(baseQuery).ToListAsync();
    }

    public async Task<HrRecommendationFunnelCounts> CountFunnelForHrAsync(Guid hrOwnerId)
    {
        var query = _context.CandidateRecommendations.AsNoTracking()
            .Where(r => r.HrOwnerId == hrOwnerId && r.IsActive)
            .Join(_context.CandidateProfiles.AsNoTracking(),
                r => r.CandidateUserId, p => p.UserId,
                (r, p) => new { r, p })
            .Where(x => x.p.AllowRecruiterRecommendation);

        return new HrRecommendationFunnelCounts
        {
            NewUnviewed = await query.CountAsync(x =>
                x.r.Status == CandidateRecommendationStatus.New && x.r.ViewedAt == null),
            Shortlisted = await query.CountAsync(x =>
                x.r.Status == CandidateRecommendationStatus.Shortlisted),
            Invited = await query.CountAsync(x =>
                x.r.Status == CandidateRecommendationStatus.Invited)
        };
    }
}
