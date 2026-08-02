using System.Text.Json;
using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Recommendation;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services;

/// <summary>Rule-based candidate recommendation + thao tác dashboard HR (SCRUM-291, SCRUM-295, SCRUM-328, SCRUM-377).</summary>
public class RecommendationService : IRecommendationService
{
    /// <summary>Ngưỡng điểm tối thiểu (thang 0–100) để tạo recommendation — rule MVP SCRUM-288.</summary>
    public const double MinScoreForRecommendation = 70;

    /// <summary>Khớp ngưỡng Speed Demon trên FE candidate profile (≤ 35 phút).</summary>
    private const int FastSessionThresholdMinutes = 35;

    private static readonly string[] FilterableStatuses =
    {
        CandidateRecommendationStatus.New,
        CandidateRecommendationStatus.Shortlisted,
        CandidateRecommendationStatus.Dismissed,
        CandidateRecommendationStatus.Invited
    };

    private static readonly HashSet<string> AllowedSortBy =
        new(StringComparer.OrdinalIgnoreCase) { "score", "date" };

    private static readonly HashSet<string> AllowedSortDir =
        new(StringComparer.OrdinalIgnoreCase) { "asc", "desc" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ICandidateRecommendationRepository _recommendationRepository;
    private readonly ICandidateInvitationRepository _invitationRepository;
    private readonly ICandidateProfileRepository _profileRepository;
    private readonly IPracticeSessionRepository _practiceSessionRepository;
    private readonly IHrCompanyInfoService _hrCompanyInfoService;
    private readonly IBlobStorageService _blobStorage;
    private readonly BlobStorageSettings _blobSettings;
    private readonly ISubscriptionGateService _subscriptionGate;

    public RecommendationService(
        ICandidateRecommendationRepository recommendationRepository,
        ICandidateInvitationRepository invitationRepository,
        ICandidateProfileRepository profileRepository,
        IPracticeSessionRepository practiceSessionRepository,
        IHrCompanyInfoService hrCompanyInfoService,
        IBlobStorageService blobStorage,
        IOptions<BlobStorageSettings> blobSettings,
        ISubscriptionGateService subscriptionGate)
    {
        _recommendationRepository = recommendationRepository;
        _invitationRepository = invitationRepository;
        _profileRepository = profileRepository;
        _practiceSessionRepository = practiceSessionRepository;
        _hrCompanyInfoService = hrCompanyInfoService;
        _blobStorage = blobStorage;
        _blobSettings = blobSettings.Value;
        _subscriptionGate = subscriptionGate;
    }

    public async Task GenerateForCompletedSessionAsync(PracticeSession session)
    {
        if (session.Status != PracticeSessionStatus.Completed)
            return;

        if (session.OverallScore is not double score || score < MinScoreForRecommendation)
            return;

        // SCRUM-382: Free soft paywall — không ghi data gợi ý HR
        if (!await _subscriptionGate.CanPersistHrRecommendationAsync(session.CandidateUserId))
            return;

        var profile = await _profileRepository.GetByUserIdAsync(session.CandidateUserId);
        if (profile is null || !profile.AllowRecruiterRecommendation)
            return;

        var questionSet = await _recommendationRepository.GetPublishedQuestionSetAsync(session.QuestionSetId);
        if (questionSet is null)
            return;

        var existing = await _recommendationRepository.GetByCandidateAndSetAsync(
            session.CandidateUserId, session.QuestionSetId);

        if (existing is null)
        {
            await _recommendationRepository.AddAsync(new CandidateRecommendation
            {
                CandidateUserId = session.CandidateUserId,
                QuestionSetId = session.QuestionSetId,
                PracticeSessionId = session.Id,
                HrOwnerId = questionSet.OwnerId,
                OverallScore = score
            });
            return;
        }

        // Làm lại nhiều lần: chỉ giữ điểm cao nhất, không reset trạng thái HR đã xử lý.
        if (score > existing.OverallScore)
        {
            existing.OverallScore = score;
            existing.PracticeSessionId = session.Id;
            existing.UpdatedAt = DateTime.UtcNow;
            await _recommendationRepository.UpdateAsync(existing);
        }
    }

    public async Task<PagedResultDto<HrRecommendationListItemDto>> ListForHrAsync(
        Guid hrUserId, HrRecommendationListQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var (status, minScore, sortBy, sortDir) = ValidateListQuery(query);

        var (rows, totalCount) = await _recommendationRepository.ListByHrAsync(
            hrUserId, page, pageSize, status, query.QuestionSetId, minScore, sortBy, sortDir);

        // Cùng 1 hrUserId -> cùng 1 công ty cho mọi dòng trong list — chỉ cần lookup 1 lần, tránh N+1 query.
        // Dùng làm fallback title khi HR chưa đặt tên riêng cho bộ câu hỏi, tránh trả về chuỗi rỗng.
        var (companyName, _) = await _hrCompanyInfoService.GetByHrUserIdAsync(hrUserId);

        return new PagedResultDto<HrRecommendationListItemDto>
        {
            Items = rows.Select(r => MapToListItem(r, companyName)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<HrRecommendationDetailDto> GetByIdForHrAsync(Guid id, Guid hrUserId)
    {
        var row = await _recommendationRepository.GetDetailRowByIdForHrAsync(id, hrUserId);
        if (row is null)
        {
            // Phân biệt 404 vs 403: tồn tại nhưng không thuộc HR → Forbidden; không tồn tại → NotFound.
            var existing = await _recommendationRepository.GetByIdAsync(id);
            if (existing is null)
                throw new NotFoundException("Recommendation không tồn tại.");
            throw new ForbiddenException("Bạn không có quyền xem recommendation này.");
        }

        var (companyName, _) = await _hrCompanyInfoService.GetByHrUserIdAsync(hrUserId);
        var stats = await _practiceSessionRepository.GetStatsAsync(row.CandidateUserId, null, null);
        var hasFastSession = await HasFastCompletedSessionAsync(row.CandidateUserId);
        var (cvSkills, cvSummary) = ParseCvEvaluation(row.CvEvaluationJson);

        var dto = new HrRecommendationDetailDto();
        CopyListFields(MapToListItem(row, companyName), dto);
        dto.AvatarUrl = row.AvatarUrl;
        dto.Bio = row.Bio;
        dto.Address = row.Address;
        dto.PhoneNumber = row.PhoneNumber;
        dto.LinkedInUrl = row.LinkedInUrl;
        dto.GithubUrl = row.GithubUrl;
        dto.HasCv = !string.IsNullOrWhiteSpace(row.CvBlobPath)
            || !string.IsNullOrWhiteSpace(row.CvFileName);
        dto.CvFileName = row.CvFileName;
        dto.CvUploadedAt = row.CvUploadedAt;
        dto.CvSummary = cvSummary;
        dto.CvSkills = cvSkills;
        dto.TotalSessions = stats.TotalSessions;
        dto.AverageScore = stats.AverageScore;
        dto.BestScore = stats.BestScore;
        dto.HasFastSession = hasFastSession;
        return dto;
    }

    public async Task<HrRecommendationCvDto> GetCvForHrAsync(Guid id, Guid hrUserId)
    {
        var row = await _recommendationRepository.GetDetailRowByIdForHrAsync(id, hrUserId);
        if (row is null)
        {
            var existing = await _recommendationRepository.GetByIdAsync(id);
            if (existing is null)
                throw new NotFoundException("Recommendation không tồn tại.");
            throw new ForbiddenException("Bạn không có quyền xem recommendation này.");
        }

        if (string.IsNullOrWhiteSpace(row.CvBlobPath))
            throw new NotFoundException("Candidate chưa tải lên CV.");

        var downloadUrl = await _blobStorage.GenerateReadSasUrlAsync(
            row.CvBlobPath, TimeSpan.FromMinutes(_blobSettings.SasExpiryMinutes));

        return new HrRecommendationCvDto
        {
            CvFileName = row.CvFileName ?? "cv",
            ContentType = row.CvContentType,
            UploadedAt = row.CvUploadedAt,
            DownloadUrl = downloadUrl
        };
    }

    public async Task<RecommendationActionResponseDto> ShortlistAsync(Guid id, Guid hrUserId)
    {
        var recommendation = await GetOwnedRecommendationAsync(id, hrUserId);

        if (recommendation.Status == CandidateRecommendationStatus.Invited)
            throw new ConflictException("Recommendation đã gửi lời mời — không thể đổi trạng thái.");

        recommendation.Status = CandidateRecommendationStatus.Shortlisted;
        recommendation.UpdatedAt = DateTime.UtcNow;
        await _recommendationRepository.UpdateAsync(recommendation);

        return new RecommendationActionResponseDto { Id = recommendation.Id, Status = recommendation.Status };
    }

    public async Task<RecommendationActionResponseDto> DismissAsync(Guid id, Guid hrUserId)
    {
        var recommendation = await GetOwnedRecommendationAsync(id, hrUserId);

        if (recommendation.Status == CandidateRecommendationStatus.Invited)
            throw new ConflictException("Recommendation đã gửi lời mời — không thể đổi trạng thái.");

        recommendation.Status = CandidateRecommendationStatus.Dismissed;
        recommendation.UpdatedAt = DateTime.UtcNow;
        await _recommendationRepository.UpdateAsync(recommendation);

        return new RecommendationActionResponseDto { Id = recommendation.Id, Status = recommendation.Status };
    }

    public async Task<InviteCandidateResponseDto> InviteAsync(Guid id, Guid hrUserId, InviteCandidateRequestDto dto)
    {
        var recommendation = await GetOwnedRecommendationAsync(id, hrUserId);

        // Check trực tiếp bảng invitation thay vì chỉ tin vào Status — tránh đâm vào unique index (500)
        // nếu lần mời trước đã tạo invitation nhưng cập nhật Status thất bại giữa chừng.
        if (recommendation.Status == CandidateRecommendationStatus.Invited
            || await _invitationRepository.GetByRecommendationIdAsync(recommendation.Id) is not null)
            throw new ConflictException("Recommendation này đã gửi lời mời trước đó.");

        if (recommendation.Status == CandidateRecommendationStatus.Dismissed)
            throw new ConflictException("Recommendation đã bị dismiss — shortlist lại trước khi mời.");

        var invitation = new CandidateInvitation
        {
            RecommendationId = recommendation.Id,
            HrUserId = hrUserId,
            CandidateUserId = recommendation.CandidateUserId,
            Message = string.IsNullOrWhiteSpace(dto.Message) ? null : dto.Message.Trim()
        };
        await _invitationRepository.AddAsync(invitation);

        recommendation.Status = CandidateRecommendationStatus.Invited;
        recommendation.UpdatedAt = DateTime.UtcNow;
        await _recommendationRepository.UpdateAsync(recommendation);

        return new InviteCandidateResponseDto
        {
            RecommendationId = recommendation.Id,
            InvitationId = invitation.Id,
            RecommendationStatus = recommendation.Status,
            InvitationStatus = invitation.Status
        };
    }

    private static (string? status, double? minScore, string sortBy, string sortDir) ValidateListQuery(
        HrRecommendationListQueryDto query)
    {
        string? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            status = query.Status.Trim().ToUpperInvariant();
            if (!FilterableStatuses.Contains(status))
                throw new BadRequestException(
                    $"Status không hợp lệ. Giá trị cho phép: {string.Join(", ", FilterableStatuses)}.");
        }

        double? minScore = query.MinScore;
        if (minScore.HasValue && (minScore.Value < 0 || minScore.Value > 100))
            throw new BadRequestException("MinScore phải nằm trong khoảng 0–100.");

        var sortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "score" : query.SortBy.Trim();
        if (!AllowedSortBy.Contains(sortBy))
            throw new BadRequestException("SortBy không hợp lệ. Giá trị cho phép: score, date.");

        var sortDir = string.IsNullOrWhiteSpace(query.SortDir) ? "desc" : query.SortDir.Trim();
        if (!AllowedSortDir.Contains(sortDir))
            throw new BadRequestException("SortDir không hợp lệ. Giá trị cho phép: asc, desc.");

        return (status, minScore, sortBy.ToLowerInvariant(), sortDir.ToLowerInvariant());
    }

    private static HrRecommendationListItemDto MapToListItem(HrRecommendationRow r, string? companyName)
    {
        var title = PublishedQuestionSetMapper.ResolveTitle(r.QuestionSetTitle, companyName ?? string.Empty);
        return new HrRecommendationListItemDto
        {
            Id = r.Id,
            CandidateUserId = r.CandidateUserId,
            CandidateName = r.CandidateName,
            CandidateEmail = r.CandidateEmail,
            TargetRole = r.TargetRole,
            SeniorityLevel = r.SeniorityLevel,
            TechStack = r.TechStack.ToList(),
            QuestionSetId = r.QuestionSetId,
            QuestionSetTitle = title,
            OverallScore = r.OverallScore,
            Status = r.Status,
            RecommendationReason = BuildRecommendationReason(r.OverallScore),
            InvitationStatus = r.InvitationStatus,
            InvitationResponseMessage = r.InvitationResponseMessage,
            InvitationSharedPhoneNumber = r.InvitationSharedPhoneNumber,
            RecommendedAt = r.RecommendedAt
        };
    }

    private static void CopyListFields(HrRecommendationListItemDto src, HrRecommendationDetailDto dest)
    {
        dest.Id = src.Id;
        dest.CandidateUserId = src.CandidateUserId;
        dest.CandidateName = src.CandidateName;
        dest.CandidateEmail = src.CandidateEmail;
        dest.TargetRole = src.TargetRole;
        dest.SeniorityLevel = src.SeniorityLevel;
        dest.TechStack = src.TechStack;
        dest.QuestionSetId = src.QuestionSetId;
        dest.QuestionSetTitle = src.QuestionSetTitle;
        dest.OverallScore = src.OverallScore;
        dest.Status = src.Status;
        dest.RecommendationReason = src.RecommendationReason;
        dest.InvitationStatus = src.InvitationStatus;
        dest.InvitationResponseMessage = src.InvitationResponseMessage;
        dest.InvitationSharedPhoneNumber = src.InvitationSharedPhoneNumber;
        dest.RecommendedAt = src.RecommendedAt;
    }

    private static (List<string> Skills, string? Summary) ParseCvEvaluation(string? evaluationJson)
    {
        if (string.IsNullOrWhiteSpace(evaluationJson))
            return (new List<string>(), null);

        try
        {
            using var doc = JsonDocument.Parse(evaluationJson);
            var skills = doc.RootElement.TryGetProperty("skills", out var skillsEl)
                ? JsonSerializer.Deserialize<List<string>>(skillsEl.GetRawText(), JsonOptions) ?? new()
                : new List<string>();
            var summary = doc.RootElement.TryGetProperty("summary", out var summaryEl)
                ? summaryEl.GetString()
                : null;
            return (skills, summary);
        }
        catch (JsonException)
        {
            return (new List<string>(), null);
        }
    }

    /// <summary>
    /// Check nhanh có phiên COMPLETED ≤ FastSessionThresholdMinutes không.
    /// TotalDurationSeconds từ stats là tổng — không đủ để suy ra từng phiên.
    /// </summary>
    private async Task<bool> HasFastCompletedSessionAsync(Guid candidateUserId)
    {
        var (items, _) = await _practiceSessionRepository.ListAsync(
            candidateUserId,
            status: PracticeSessionStatus.Completed,
            questionSetId: null,
            keyword: null,
            fromDate: null,
            toDate: null,
            page: 1,
            pageSize: 50);

        var thresholdSeconds = FastSessionThresholdMinutes * 60;
        return items.Any(s =>
            s.StartedAt.HasValue
            && s.CompletedAt.HasValue
            && (s.CompletedAt.Value - s.StartedAt.Value).TotalSeconds > 0
            && (s.CompletedAt.Value - s.StartedAt.Value).TotalSeconds <= thresholdSeconds);
    }

    /// <summary>Lý do ephemeral từ score — không gọi AI, không lưu DB (SCRUM-328).</summary>
    public static string BuildRecommendationReason(double score)
        => $"Đạt {score:0}/100 trên bộ câu hỏi đã publish";

    private async Task<CandidateRecommendation> GetOwnedRecommendationAsync(Guid id, Guid hrUserId)
    {
        var recommendation = await _recommendationRepository.GetByIdAsync(id);
        if (recommendation is null)
            throw new NotFoundException("Recommendation không tồn tại.");
        if (recommendation.HrOwnerId != hrUserId)
            throw new ForbiddenException("Bạn không có quyền thao tác recommendation này.");
        return recommendation;
    }
}
