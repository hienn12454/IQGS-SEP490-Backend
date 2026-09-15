using System.Text.Json;
using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Logging;
using QuestionCompletionXpContext = ApplicationLayer.DTOs.Gamification.QuestionCompletionXpContext;
using QuestionSetCompletionXpContext = ApplicationLayer.DTOs.Gamification.QuestionSetCompletionXpContext;
using XpRewardDto = ApplicationLayer.DTOs.Gamification.XpRewardDto;

namespace ApplicationLayer.Services;

public class CandidatePracticeSessionService : ICandidatePracticeSessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPracticeSessionRepository _sessionRepository;
    private readonly ICandidateMarketplaceRepository _marketplaceRepository;
    private readonly ICandidateAnswerRepository _answerRepository;
    private readonly IAiFeedbackRepository _feedbackRepository;
    private readonly IRagService _ragService;
    private readonly IRecommendationService _recommendationService;
    private readonly ISubscriptionGateService _subscriptionGate;
    private readonly IUsageMeteringService _usageMetering;
    private readonly IBlobStorageService _blobStorage;
    private readonly IGamificationService _gamificationService;
    private readonly ICandidatePersonalSetJobRepository _personalSetJobs;
    private readonly ICoachCompetencyService _coachCompetency;
    private readonly IPlatformSettingsRepository _platformSettingsRepository;
    private readonly ILogger<CandidatePracticeSessionService> _logger;

    /// <summary>SCRUM-446: debounce giữa 2 lần ghi nhận rời tab (tránh spam visibilitychange).</summary>
    private static readonly TimeSpan TabLeaveDebounce = TimeSpan.FromSeconds(2);

    public CandidatePracticeSessionService(
        IPracticeSessionRepository sessionRepository,
        ICandidateMarketplaceRepository marketplaceRepository,
        ICandidateAnswerRepository answerRepository,
        IAiFeedbackRepository feedbackRepository,
        IRagService ragService,
        IRecommendationService recommendationService,
        ISubscriptionGateService subscriptionGate,
        IUsageMeteringService usageMetering,
        IBlobStorageService blobStorage,
        IGamificationService gamificationService,
        ICandidatePersonalSetJobRepository personalSetJobs,
        ICoachCompetencyService coachCompetency,
        IPlatformSettingsRepository platformSettingsRepository,
        ILogger<CandidatePracticeSessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _marketplaceRepository = marketplaceRepository;
        _answerRepository = answerRepository;
        _feedbackRepository = feedbackRepository;
        _ragService = ragService;
        _recommendationService = recommendationService;
        _subscriptionGate = subscriptionGate;
        _usageMetering = usageMetering;
        _blobStorage = blobStorage;
        _gamificationService = gamificationService;
        _personalSetJobs = personalSetJobs;
        _coachCompetency = coachCompetency;
        _platformSettingsRepository = platformSettingsRepository;
        _logger = logger;
    }

    /// <summary>Tạo phiên mới, hoặc trả về phiên IN_PROGRESS đã có cho cùng bộ câu hỏi (resume — AC-02 SCRUM-298).</summary>
    public async Task<PracticeSessionResponseDto> StartAsync(Guid questionSetId, Guid candidateUserId)
    {
        if (!await _marketplaceRepository.CanCandidateStartAsync(questionSetId, candidateUserId))
            throw new NotFoundException("Bộ câu hỏi không tồn tại hoặc chưa được publish.");

        var existingSession = await _sessionRepository.GetInProgressByQuestionSetAsync(candidateUserId, questionSetId);
        if (existingSession is not null)
        {
            var timeLimit = await _sessionRepository.GetTimeLimitMinutesAsync(questionSetId);
            if (await AutoSubmitIfExpiredAsync(existingSession, timeLimit) is null)
                return await BuildSessionResponseAsync(existingSession);
            // Phiên dở dang đã hết giờ và vừa được tự động nộp — bắt đầu phiên mới bên dưới.
        }

        // SCRUM-446: snapshot luật anti-cheat lúc start — đổi setting Admin không ảnh hưởng phiên đang chạy.
        var platformSettings = await _platformSettingsRepository.GetAsync();

        var session = new PracticeSession
        {
            CandidateUserId = candidateUserId,
            QuestionSetId = questionSetId,
            Status = PracticeSessionStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            AntiCheatEnabled = platformSettings.AntiCheatEnabled,
            AntiCheatMaxTabLeaves = PracticeAntiCheatRules.ClampMaxTabLeaves(platformSettings.AntiCheatMaxTabLeaves),
            TabLeaveCount = 0
        };
        await _sessionRepository.AddAsync(session);

        return await BuildSessionResponseAsync(session);
    }

    public async Task<PracticeSessionResponseDto> GetByIdAsync(Guid sessionId, Guid candidateUserId)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);
        EnsureSessionNotAbandoned(session);
        _ = await AutoSubmitIfExpiredAsync(session, await _sessionRepository.GetTimeLimitMinutesAsync(session.QuestionSetId));
        return await BuildSessionResponseAsync(session);
    }

    public async Task<SubmitAnswerResponseDto> SubmitAnswerAsync(
        Guid sessionId, Guid candidateUserId, SubmitAnswerDto dto)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);
        EnsureSessionNotAbandoned(session);

        if (await AutoSubmitIfExpiredAsync(session, await _sessionRepository.GetTimeLimitMinutesAsync(session.QuestionSetId)) is not null)
            throw new BadRequestException("Đã hết thời gian làm bài — phiên đã được tự động nộp.");

        if (session.Status != PracticeSessionStatus.InProgress)
            throw new BadRequestException("Chỉ có thể nộp câu trả lời khi phiên đang ở trạng thái IN_PROGRESS.");

        if (!await _marketplaceRepository.QuestionBelongsToSetAsync(dto.QuestionId, session.QuestionSetId))
            throw new BadRequestException("questionId không thuộc bộ câu hỏi của phiên luyện tập này.");

        // SCRUM-332: Free làm full bài — chỉ validate câu thuộc set, không gate feedback giữa phiên
        var questions = await _marketplaceRepository.GetQuestionsSnapshotAsync(session.QuestionSetId);
        if (questions.All(q => q.Id != dto.QuestionId))
            throw new BadRequestException("questionId không thuộc bộ câu hỏi của phiên luyện tập này.");

        var answerText = dto.AnswerText.Trim();
        var submittedAt = DateTime.UtcNow;

        // Chỉ lưu answer — AI evaluate dời sang complete (SCRUM-332)
        var existing = await _answerRepository.GetAsync(sessionId, dto.QuestionId);
        if (existing is null)
        {
            await _answerRepository.AddAsync(new CandidateAnswer
            {
                PracticeSessionId = sessionId,
                QuestionSetQuestionId = dto.QuestionId,
                AnswerText = answerText,
                SubmittedAt = submittedAt
            });
        }
        else
        {
            existing.AnswerText = answerText;
            existing.SubmittedAt = submittedAt;
            await _answerRepository.UpdateAsync(existing);
        }

        return new SubmitAnswerResponseDto
        {
            QuestionId = dto.QuestionId,
            AnswerText = answerText,
            SubmittedAt = submittedAt,
            EvaluationStatus = AiFeedbackEvaluationStatus.Pending,
            Score = null,
            Strengths = [],
            Improvements = [],
            Suggestion = null,
            DimensionScores = null,
            EvaluationError = null
        };
    }

    public async Task<PracticeSessionCompleteResponseDto> CompleteAsync(Guid sessionId, Guid candidateUserId)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);
        EnsureSessionNotAbandoned(session);

        // Hết giờ thì phiên đã được tự nộp tại đúng deadline — candidate bấm nộp sau đó vẫn nhận kết quả bình thường.
        var xpRewards = await AutoSubmitIfExpiredAsync(session, await _sessionRepository.GetTimeLimitMinutesAsync(session.QuestionSetId));
        if (xpRewards is null)
        {
            if (session.Status != PracticeSessionStatus.InProgress)
                throw new BadRequestException("Chỉ có thể hoàn thành phiên đang ở trạng thái IN_PROGRESS.");

            session.Status = PracticeSessionStatus.Completed;
            session.CompletedAt = DateTime.UtcNow;
            xpRewards = await FinalizeCompletedSessionAsync(session);
        }

        return new PracticeSessionCompleteResponseDto
        {
            SessionId = session.Id,
            Status = session.Status,
            CompletedAt = session.CompletedAt,
            OverallScore = session.OverallScore,
            XpReward = MergeXpRewards(xpRewards),
            DurationSeconds = ComputeDurationSeconds(session.StartedAt, session.CompletedAt),
            AiInsight = MapAiInsight(session)
        };
    }

    /// <inheritdoc />
    public async Task FinalizeExpiredByWatchdogAsync(Guid sessionId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session is null || session.Status != PracticeSessionStatus.InProgress)
            return;

        var timeLimit = await _sessionRepository.GetTimeLimitMinutesAsync(session.QuestionSetId);
        var expiresAt = ComputeExpiresAt(session.StartedAt, timeLimit);
        if (expiresAt is null || DateTime.UtcNow < expiresAt)
            return;

        session.Status = PracticeSessionStatus.Completed;
        session.CompletedAt = expiresAt;
        session.UpdatedAt = DateTime.UtcNow;
        await FinalizeCompletedSessionAsync(session);
    }

    /// <summary>Candidate chủ động bỏ phiên đang làm dở (MVP optional — SCRUM-298).</summary>
    public async Task<PracticeSessionResponseDto> AbandonAsync(Guid sessionId, Guid candidateUserId)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);

        if (await AutoSubmitIfExpiredAsync(session, await _sessionRepository.GetTimeLimitMinutesAsync(session.QuestionSetId)) is not null)
            throw new BadRequestException("Phiên đã hết thời gian và được tự động nộp — không thể huỷ.");

        if (session.Status != PracticeSessionStatus.InProgress)
            throw new BadRequestException("Chỉ có thể huỷ phiên đang ở trạng thái IN_PROGRESS.");

        // SCRUM-446: anti-cheat ON → không cho “Save & Exit / abandon rồi làm tiếp” như luyện tập thoải mái.
        if (session.AntiCheatEnabled)
            throw new BadRequestException("Phiên đang bật chống gian lận — không thể huỷ giữa chừng. Hãy nộp bài hoặc tiếp tục làm.");

        session.Status = PracticeSessionStatus.Abandoned;
        await _sessionRepository.UpdateAsync(session);

        return await BuildSessionResponseAsync(session);
    }

    /// <summary>
    /// SCRUM-446: FE báo rời tab. Chỉ đếm khi snapshot AntiCheatEnabled;
    /// debounce 2s; đủ AntiCheatMaxTabLeaves → tự Complete như hết giờ.
    /// </summary>
    public async Task<PracticeIntegrityEventResponseDto> ReportIntegrityEventAsync(
        Guid sessionId, Guid candidateUserId, PracticeIntegrityEventDto dto)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);
        var eventType = (dto.EventType ?? "TAB_HIDDEN").Trim().ToUpperInvariant();

        if (eventType != "TAB_HIDDEN")
            throw new BadRequestException("eventType không hợp lệ. Hiện hỗ trợ: TAB_HIDDEN.");

        // Hết giờ trước → nộp theo timer, không tính thêm tab leave.
        _ = await AutoSubmitIfExpiredAsync(session, await _sessionRepository.GetTimeLimitMinutesAsync(session.QuestionSetId));

        var now = DateTime.UtcNow;
        if (!PracticeAntiCheatRules.ShouldCountTabLeave(
                session.AntiCheatEnabled, session.Status, session.LastTabLeaveAt, now, TabLeaveDebounce))
        {
            return new PracticeIntegrityEventResponseDto
            {
                SessionId = session.Id,
                Status = session.Status,
                AntiCheatEnabled = session.AntiCheatEnabled,
                AntiCheatMaxTabLeaves = session.AntiCheatMaxTabLeaves,
                TabLeaveCount = session.TabLeaveCount,
                AutoSubmitted = session.Status == PracticeSessionStatus.Completed,
                Ignored = true
            };
        }

        session.TabLeaveCount += 1;
        session.LastTabLeaveAt = now;
        session.UpdatedAt = now;

        var shouldAutoSubmit = PracticeAntiCheatRules.ShouldAutoSubmit(
            session.TabLeaveCount, session.AntiCheatMaxTabLeaves);

        if (shouldAutoSubmit)
        {
            session.Status = PracticeSessionStatus.Completed;
            session.CompletedAt = now;
            await FinalizeCompletedSessionAsync(session);
            _logger.LogInformation(
                "SCRUM-446: session {SessionId} tự nộp vì rời tab {Count}/{Max}.",
                session.Id, session.TabLeaveCount, session.AntiCheatMaxTabLeaves);
        }
        else
        {
            await _sessionRepository.UpdateAsync(session);
        }

        return new PracticeIntegrityEventResponseDto
        {
            SessionId = session.Id,
            Status = session.Status,
            AntiCheatEnabled = session.AntiCheatEnabled,
            AntiCheatMaxTabLeaves = session.AntiCheatMaxTabLeaves,
            TabLeaveCount = session.TabLeaveCount,
            AutoSubmitted = shouldAutoSubmit,
            Ignored = false
        };
    }

    public async Task<PracticeSessionFeedbackDto> GetFeedbackAsync(Guid sessionId, Guid candidateUserId)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);
        var questions = await _marketplaceRepository.GetQuestionsSnapshotAsync(session.QuestionSetId);
        var answers = await _answerRepository.GetEntitiesBySessionIdAsync(sessionId);
        var feedbacks = await _feedbackRepository.GetBySessionIdAsync(sessionId);

        var canDetailed = await _subscriptionGate.CanDetailedAiFeedbackAsync(candidateUserId);
        return PracticeSessionFeedbackMapper.Map(
            session, questions, answers, feedbacks, lockTeaser: !canDetailed);
    }

    public async Task<PagedResultDto<PracticeSessionListItemDto>> ListAsync(
        Guid candidateUserId, PracticeSessionListQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var status = string.IsNullOrWhiteSpace(query.Status) ? PracticeSessionStatus.Completed : query.Status.Trim();

        var (from, to) = DateRangeFilterHelper.Resolve(query.FromDate, query.ToDate, query.Year, query.Month);

        var (rows, totalCount) = await _sessionRepository.ListAsync(
            candidateUserId, status, query.QuestionSetId, query.Keyword, from, to, page, pageSize);

        var items = rows.Select(r => new PracticeSessionListItemDto
        {
            SessionId = r.SessionId,
            QuestionSetId = r.QuestionSetId,
            SetTitle = string.IsNullOrWhiteSpace(r.SetTitle) ? r.CompanyName : r.SetTitle,
            CompanyName = r.CompanyName,
            CompanyLogo = CompanyLogoResolver.Resolve(r.CompanyLogo, r.CompanyWebsite, r.CompanyName),
            Status = r.Status,
            Score = r.Score,
            DurationSeconds = ComputeDurationSeconds(r.StartedAt, r.CompletedAt),
            StartedAt = r.StartedAt,
            CompletedAt = r.CompletedAt
        }).ToList();

        return new PagedResultDto<PracticeSessionListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PracticeSessionStatsDto> GetStatsAsync(Guid candidateUserId, PracticeSessionStatsQueryDto query)
    {
        var (from, to) = DateRangeFilterHelper.Resolve(query.FromDate, query.ToDate, query.Year, query.Month);
        return await _sessionRepository.GetStatsAsync(candidateUserId, from, to);
    }

    public Task<IReadOnlyList<CandidateSkillStatDto>> GetSkillStatsAsync(Guid candidateUserId)
        => _sessionRepository.ListSkillStatsAsync(candidateUserId);

    private async Task<(
        string EvaluationStatus,
        double? Score,
        List<string> Strengths,
        List<string> Improvements,
        string? Suggestion,
        Dictionary<string, double>? DimensionScores,
        string? ErrorMessage)> EvaluateAndPersistAsync(
        CandidateAnswer answer,
        Guid questionId,
        string? scoringMode = null)
    {
        var rubric = await _marketplaceRepository.GetQuestionEvaluationRubricAsync(questionId);
        if (rubric is null)
        {
            await UpsertFeedbackAsync(answer.Id, AiFeedbackEvaluationStatus.Failed, null, [], [], null, null,
                "Không tìm thấy câu hỏi để đánh giá.");
            return (AiFeedbackEvaluationStatus.Failed, null, [], [], null, null, "Không tìm thấy câu hỏi để đánh giá.");
        }

        try
        {
            var criteria = ParseCriteriaList(rubric.EvaluationCriteriaJson);
            var rubricDoc = RubricNormalizer.NormalizeFromJson(rubric.EvaluationCriteriaJson);
            if (RubricNormalizer.IsPublishReady(rubricDoc) || rubricDoc.Criteria.Count > 0)
                criteria = RubricNormalizer.FlattenForEvaluate(rubricDoc);
            var ragResult = await _ragService.EvaluateAnswerAsync(new EvaluateAnswerRequest
            {
                Question = rubric.Question,
                EvaluationCriteria = criteria,
                CandidateAnswer = answer.AnswerText,
                SampleAnswer = rubric.SampleAnswer,
                Skill = rubric.Skill,
                QuestionType = rubric.QuestionType,
                ScoringMode = scoringMode
            });

            if (!ragResult.Success)
            {
                var err = ragResult.Error ?? ragResult.Detail ?? "RAG evaluate thất bại.";
                await UpsertFeedbackAsync(answer.Id, AiFeedbackEvaluationStatus.Failed, null, [], [], null, null, err);
                return (AiFeedbackEvaluationStatus.Failed, null, [], [], null, null, err);
            }

            var strengths = ragResult.Strengths ?? [];
            var improvements = ragResult.Improvements ?? [];
            var roundedDimensionScores = RoundDimensionScores(ragResult.DimensionScores);

            // Coach: overall score UI = trung bình dimensions nếu LLM không trả score
            double? rawScore = ragResult.Score;
            if (rawScore is null
                && string.Equals(scoringMode, "coach", StringComparison.OrdinalIgnoreCase)
                && roundedDimensionScores is { Count: > 0 })
            {
                rawScore = roundedDimensionScores.Values.Average();
            }

            if (rawScore is null)
            {
                var err = "RAG evaluate không trả về score.";
                await UpsertFeedbackAsync(answer.Id, AiFeedbackEvaluationStatus.Failed, null, [], [], null, null, err);
                return (AiFeedbackEvaluationStatus.Failed, null, [], [], null, null, err);
            }

            var roundedScore = RoundScore(rawScore);

            await UpsertFeedbackAsync(
                answer.Id,
                AiFeedbackEvaluationStatus.Succeeded,
                roundedScore,
                strengths,
                improvements,
                ragResult.Suggestion,
                roundedDimensionScores,
                null);

            return (
                AiFeedbackEvaluationStatus.Succeeded,
                roundedScore,
                strengths,
                improvements,
                ragResult.Suggestion,
                roundedDimensionScores,
                null);
        }
        catch (Exception ex)
        {
            // AC-03: timeout / RAG unavailable — answer đã lưu, chỉ báo Failed
            var message = ex.Message;
            await UpsertFeedbackAsync(answer.Id, AiFeedbackEvaluationStatus.Failed, null, [], [], null, null, message);
            return (AiFeedbackEvaluationStatus.Failed, null, [], [], null, null, message);
        }
    }

    private async Task UpsertFeedbackAsync(
        Guid candidateAnswerId,
        string status,
        double? score,
        List<string> strengths,
        List<string> improvements,
        string? suggestion,
        Dictionary<string, double>? dimensionScores,
        string? errorMessage)
    {
        var existing = await _feedbackRepository.GetByCandidateAnswerIdAsync(candidateAnswerId);
        var strengthsJson = JsonSerializer.Serialize(strengths, JsonOptions);
        var improvementsJson = JsonSerializer.Serialize(improvements, JsonOptions);
        var dimensionJson = dimensionScores is null
            ? null
            : JsonSerializer.Serialize(dimensionScores, JsonOptions);

        if (existing is null)
        {
            await _feedbackRepository.AddAsync(new AiFeedback
            {
                CandidateAnswerId = candidateAnswerId,
                Score = score,
                StrengthsJson = strengthsJson,
                ImprovementsJson = improvementsJson,
                Suggestion = suggestion,
                DimensionScoresJson = dimensionJson,
                EvaluationStatus = status,
                ErrorMessage = errorMessage
            });
            return;
        }

        existing.Score = score;
        existing.StrengthsJson = strengthsJson;
        existing.ImprovementsJson = improvementsJson;
        existing.Suggestion = suggestion;
        existing.DimensionScoresJson = dimensionJson;
        existing.EvaluationStatus = status;
        existing.ErrorMessage = errorMessage;
        await _feedbackRepository.UpdateAsync(existing);
    }

    private async Task<PracticeSession> GetOwnedSessionAsync(Guid sessionId, Guid candidateUserId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new NotFoundException("Phiên luyện tập không tồn tại.");

        if (session.CandidateUserId != candidateUserId)
            throw new ForbiddenException("Bạn không có quyền truy cập phiên luyện tập này.");

        return session;
    }

    /// <summary>SCRUM-408: HR unpublish → phiên ABANDONED, không làm tiếp được.</summary>
    private static void EnsureSessionNotAbandoned(PracticeSession session)
    {
        if (session.Status == PracticeSessionStatus.Abandoned)
            throw new BadRequestException("Bộ câu hỏi đã được gỡ; phiên đã hủy.");
    }

    /// <summary>Làm tròn điểm AI trả về hàng đơn vị (vd 51.0739292829 → 51) — không giữ chữ số thập phân.</summary>
    private static double? RoundScore(double? score) => score.HasValue ? Math.Round(score.Value, 0) : null;

    private static Dictionary<string, double>? RoundDimensionScores(Dictionary<string, double>? dimensionScores) =>
        dimensionScores?.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 0));

    /// <summary>Hạn chót nộp bài = StartedAt + giới hạn phút — null nếu bộ không giới hạn thời gian.</summary>
    private static DateTime? ComputeExpiresAt(DateTime? startedAt, int? timeLimitMinutes)
        => startedAt.HasValue && timeLimitMinutes.HasValue
            ? startedAt.Value.AddMinutes(timeLimitMinutes.Value)
            : null;

    /// <summary>
    /// Hết giờ làm bài → tự động nộp: chuyển COMPLETED với CompletedAt = đúng deadline (không tính thời gian trễ),
    /// giữ nguyên các câu trả lời đã submit và chấm điểm như nộp tay.
    /// Trả về danh sách XP reward vừa award nếu vừa tự nộp — null nếu phiên chưa hết giờ / đã xong từ trước.
    /// </summary>
    private async Task<List<XpRewardDto>?> AutoSubmitIfExpiredAsync(PracticeSession session, int? timeLimitMinutes)
    {
        if (session.Status != PracticeSessionStatus.InProgress)
            return null;

        var expiresAt = ComputeExpiresAt(session.StartedAt, timeLimitMinutes);
        if (expiresAt is null || DateTime.UtcNow < expiresAt)
            return null;

        session.Status = PracticeSessionStatus.Completed;
        session.CompletedAt = expiresAt;
        session.UpdatedAt = DateTime.UtcNow;
        return await FinalizeCompletedSessionAsync(session);
    }

    /// <summary>
    /// SCRUM-332 + Teaser Freemium: evaluate answers theo entitlement, rồi overall / insight / recommendation.
    /// Đồng thời award Gamification XP (QuestionCompleted/ScoreBonus/ImprovementBonus theo từng câu vừa chấm
    /// AI thành công + QuestionSetCompleted khi phiên hoàn thành) — trả về mọi XpRewardDto vừa phát sinh để
    /// CompleteAsync gộp lại trả FE. Gamification lỗi không được làm fail việc hoàn thành phiên.
    /// </summary>
    private async Task<List<XpRewardDto>> FinalizeCompletedSessionAsync(PracticeSession session)
    {
        var xpRewards = await EvaluateAnswersForSessionAsync(session);

        var canDetailed = await _subscriptionGate.CanDetailedAiFeedbackAsync(session.CandidateUserId);
        if (canDetailed)
        {
            session.OverallScore = await ComputeSessionOverallScoreAsync(session);
            await TryGenerateAiInsightAsync(session);
            await _sessionRepository.UpdateAsync(session);
            await TryGenerateRecommendationAsync(session);
        }
        else
        {
            // Free: overall = điểm câu teaser (không chia cho N câu — tránh điểm bị kéo thấp giả tạo)
            session.OverallScore = await ComputeTeaserOverallScoreAsync(session);
            await _sessionRepository.UpdateAsync(session);
            // Free không sinh insight đầy đủ / không persist recommendation HR
        }

        // Coach diagnostic/reassessment/drill phải ghi competency + roadmap kể cả khi teaser gate tắt detailed feedback.
        await TryUpsertCoachPlanAsync(session);

        var setReward = await TryAwardQuestionSetCompletionXpAsync(session);
        if (setReward is not null)
            xpRewards.Add(setReward);

        return xpRewards;
    }

    private async Task TryUpsertCoachPlanAsync(PracticeSession session)
    {
        try
        {
            var job = await _personalSetJobs.GetByQuestionSetIdIncludingInactiveAsync(session.QuestionSetId);
            var isCoachDiagnostic = job is not null
                && job.CandidateUserId == session.CandidateUserId
                && (string.Equals(job.Purpose, CandidatePersonalSetPurpose.CvDiagnostic, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(job.Purpose, CandidatePersonalSetPurpose.CvReassessment, StringComparison.OrdinalIgnoreCase));

            var scoreResult = await _coachCompetency.ScoreAssessmentFromSessionAsync(session);
            if (isCoachDiagnostic)
            {
                if (scoreResult.Scored)
                {
                    _logger.LogInformation(
                        "Coach scoring OK session {SessionId} assessment {AssessmentId} roadmapUpdated={RoadmapUpdated}",
                        session.Id, scoreResult.AssessmentId, scoreResult.RoadmapUpdated);
                    if (!scoreResult.RoadmapUpdated)
                    {
                        _logger.LogError(
                            "Assessment {AssessmentId} đã Scored nhưng dựng roadmap thất bại (session {SessionId}).",
                            scoreResult.AssessmentId, session.Id);
                    }
                }
                else
                {
                    _logger.LogError(
                        "Coach diagnostic session {SessionId} / set {SetId} không Scored: {Reason}",
                        session.Id, session.QuestionSetId, scoreResult.SkipReason);
                }
            }

            await _coachCompetency.HandleDrillSessionCompletedAsync(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không cập nhật Coach competency sau session {SessionId}", session.Id);
        }
    }

    /// <summary>
    /// Chấm lại diagnostic gần nhất từ session COMPLETED — dùng khi Complete nuốt lỗi scoring
    /// hoặc Candidate mở Báo cáo/Lộ trình trước khi persist xong.
    /// </summary>
    public async Task RescoreLatestCoachDiagnosticAsync(Guid candidateUserId)
    {
        var jobs = await _personalSetJobs.ListByCandidateAsync(candidateUserId);
        var diagnosticJobs = jobs
            .Where(j =>
                j.QuestionSetId is Guid
                && j.CandidateUserId == candidateUserId
                && (string.Equals(j.Purpose, CandidatePersonalSetPurpose.CvDiagnostic, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(j.Purpose, CandidatePersonalSetPurpose.CvReassessment, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Exception? lastError = null;
        string? lastSkip = null;
        foreach (var job in diagnosticJobs)
        {
            var setId = job.QuestionSetId!.Value;
            var session = await _sessionRepository.GetBestCompletedSessionOnSetAsync(candidateUserId, setId);
            if (session is null) continue;
            try
            {
                var result = await _coachCompetency.ScoreAssessmentFromSessionAsync(session);
                if (result.Scored)
                {
                    _logger.LogInformation(
                        "Rescore Coach OK user {UserId} assessment {AssessmentId} roadmapUpdated={RoadmapUpdated}",
                        candidateUserId, result.AssessmentId, result.RoadmapUpdated);
                    return;
                }

                lastSkip = result.SkipReason;
                _logger.LogWarning(
                    "Rescore Coach skip session {SessionId}: {Reason} — thử job tiếp theo",
                    session.Id, result.SkipReason);
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogError(ex, "Rescore Coach thất bại cho session {SessionId} / set {SetId}", session.Id, setId);
            }
        }

        if (lastError is not null)
            throw lastError;

        throw new BadRequestException(
            lastSkip is not null
                ? $"Không chấm lại được bài Coach: {lastSkip}. Hãy hoàn thành lại bài chẩn đoán."
                : "Không tìm thấy phiên luyện tập đã hoàn thành cho bài chẩn đoán Coach để chấm lại.");
    }

    /// <summary>
    /// Premium: chấm mọi answer. Free: chỉ chấm FreeTeaserFeedbackCount câu (ưu tiên answer dài nhất).
    /// Idempotent nếu đã có Succeeded feedback. Với mỗi câu có kết quả AI chấm Succeeded (kể cả câu đã chấm
    /// từ lần gọi trước — retry-safe) thử award Gamification XP; câu bị skip-gate (rỗng/spam, auto score 0)
    /// KHÔNG được coi là hoạt động luyện tập có ý nghĩa nên không award XP.
    /// </summary>
    private async Task<List<XpRewardDto>> EvaluateAnswersForSessionAsync(PracticeSession session)
    {
        var xpRewards = new List<XpRewardDto>();

        var questions = (await _marketplaceRepository.GetQuestionsSnapshotAsync(session.QuestionSetId))
            .OrderBy(q => q.Order)
            .ToList();
        var answers = await _answerRepository.GetEntitiesBySessionIdAsync(session.Id);
        if (answers.Count == 0)
            return xpRewards;

        var feedbacks = await _feedbackRepository.GetBySessionIdAsync(session.Id);
        var feedbackByAnswerId = feedbacks.ToDictionary(f => f.CandidateAnswerId);

        var canDetailed = await _subscriptionGate.CanDetailedAiFeedbackAsync(session.CandidateUserId);
        IEnumerable<CandidateAnswer> toEvaluate = answers;

        if (!canDetailed)
        {
            var teaserCount = await _subscriptionGate.GetFreeTeaserFeedbackCountAsync(session.CandidateUserId);
            // Ưu tiên câu có nội dung dài nhất (meaningful) — fallback Order nếu bằng nhau
            toEvaluate = answers
                .OrderByDescending(a => a.AnswerText?.Trim().Length ?? 0)
                .ThenBy(a => questions.FindIndex(q => q.Id == a.QuestionSetQuestionId))
                .Take(teaserCount)
                .ToList();
        }

        foreach (var answer in toEvaluate)
        {
            var answerText = answer.AnswerText?.Trim() ?? string.Empty;
            // Pure/deterministic theo answerText — tính lại được ở cả câu đã có feedback Succeeded từ trước,
            // để phân biệt "AI chấm thật" với "skip-gate auto score 0" khi retry (không dựa vào field đã lưu).
            var isSkipGated = AnswerEvaluationGate.TrySkip(answerText, out var skipReason);

            double? score;
            if (feedbackByAnswerId.TryGetValue(answer.Id, out var existing)
                && existing.EvaluationStatus == AiFeedbackEvaluationStatus.Succeeded)
            {
                score = existing.Score;
            }
            else if (isSkipGated)
            {
                _logger.LogInformation(
                    "Bỏ qua RAG evaluate cho answer {AnswerId}: {Reason}",
                    answer.Id, skipReason);

                await UpsertFeedbackAsync(
                    answer.Id,
                    AiFeedbackEvaluationStatus.Succeeded,
                    score: 0,
                    strengths: [],
                    improvements: [skipReason],
                    suggestion: skipReason,
                    dimensionScores: null,
                    errorMessage: null);
                continue;
            }
            else
            {
                // SCRUM-447: Coach assessment dùng dimension scoring; marketplace giữ score LLM
                string? scoringMode = null;
                var coachJob = await _personalSetJobs.GetByQuestionSetIdAsync(session.QuestionSetId);
                if (coachJob is not null
                    && coachJob.Purpose is CandidatePersonalSetPurpose.CvDiagnostic
                        or CandidatePersonalSetPurpose.CvReassessment)
                {
                    scoringMode = "coach";
                }

                var (evalStatus, evalScore, _, _, _, _, _) =
                    await EvaluateAndPersistAsync(answer, answer.QuestionSetQuestionId, scoringMode);
                await _usageMetering.IncrementAsync(session.CandidateUserId, UsageType.CandidateFeedback);

                if (evalStatus != AiFeedbackEvaluationStatus.Succeeded)
                    continue;

                score = evalScore;
            }

            if (isSkipGated || !score.HasValue)
                continue;

            var reward = await TryAwardQuestionCompletionXpAsync(session, answer, score.Value, questions);
            if (reward is not null)
                xpRewards.Add(reward);
        }

        return xpRewards;
    }

    /// <summary>Award QuestionCompleted/ScoreBonus/ImprovementBonus cho 1 câu vừa có điểm AI Succeeded. Lỗi gamification không làm fail việc chấm bài.</summary>
    private async Task<XpRewardDto?> TryAwardQuestionCompletionXpAsync(
        PracticeSession session, CandidateAnswer answer, double score, IReadOnlyList<PublishedQuestionRow> questions)
    {
        try
        {
            var questionType = questions.FirstOrDefault(q => q.Id == answer.QuestionSetQuestionId)?.QuestionType ?? string.Empty;
            var previousBestScore = await _feedbackRepository.GetPreviousBestSucceededScoreAsync(
                session.CandidateUserId, answer.QuestionSetQuestionId, session.Id);

            return await _gamificationService.AwardQuestionCompletionAsync(new QuestionCompletionXpContext
            {
                UserId = session.CandidateUserId,
                QuestionSetQuestionId = answer.QuestionSetQuestionId,
                CandidateAnswerId = answer.Id,
                QuestionSetId = session.QuestionSetId,
                PracticeSessionId = session.Id,
                Score = score,
                QuestionType = questionType,
                PreviousBestScore = previousBestScore,
                OccurredAtUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Award QuestionCompleted XP thất bại cho answer {AnswerId} (session {SessionId}) — không ảnh hưởng kết quả chấm bài.",
                answer.Id, session.Id);
            return null;
        }
    }

    /// <summary>Award QuestionSetCompleted (+20 XP mặc định) khi phiên chuyển COMPLETED. Lỗi gamification không làm fail việc hoàn thành phiên.</summary>
    private async Task<XpRewardDto?> TryAwardQuestionSetCompletionXpAsync(PracticeSession session)
    {
        try
        {
            return await _gamificationService.AwardQuestionSetCompletionAsync(new QuestionSetCompletionXpContext
            {
                UserId = session.CandidateUserId,
                PracticeSessionId = session.Id,
                QuestionSetId = session.QuestionSetId,
                OccurredAtUtc = session.CompletedAt ?? DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Award QuestionSetCompleted XP thất bại cho session {SessionId} — không ảnh hưởng kết quả hoàn thành phiên.",
                session.Id);
            return null;
        }
    }

    /// <summary>Gộp mọi XpRewardDto phát sinh trong 1 lần Complete() (nhiều câu + set-completion) thành 1 tổng để trả FE.</summary>
    private static XpRewardDto? MergeXpRewards(IReadOnlyList<XpRewardDto> rewards)
    {
        if (rewards.Count == 0)
            return null;

        var first = rewards[0];
        var last = rewards[^1];

        return new XpRewardDto
        {
            TotalEarned = rewards.Sum(r => r.TotalEarned),
            Rewards = rewards.SelectMany(r => r.Rewards).ToList(),
            PreviousLevel = first.PreviousLevel,
            CurrentLevel = last.CurrentLevel,
            PreviousTotalXp = first.PreviousTotalXp,
            CurrentTotalXp = last.CurrentTotalXp,
            LevelUp = last.CurrentLevel > first.PreviousLevel,
            UnlockedAchievementCodes = rewards.SelectMany(r => r.UnlockedAchievementCodes).Distinct().ToList(),
            Progress = last.Progress
        };
    }

    /// <summary>Free overall = điểm Succeeded của (các) câu teaser — lấy trung bình nếu >1 teaser.</summary>
    private async Task<double?> ComputeTeaserOverallScoreAsync(PracticeSession session)
    {
        var scores = await _feedbackRepository.GetSucceededScoresAsync(session.Id);
        var list = scores.ToList();
        if (list.Count == 0)
            return null;
        return Math.Round(list.Average(), 0);
    }

    /// <summary>SCRUM-305 — gọi RAG sinh insight song ngữ; lỗi → log, để null (FE fallback).</summary>
    private async Task TryGenerateAiInsightAsync(PracticeSession session)
    {
        try
        {
            var questions = await _marketplaceRepository.GetQuestionsSnapshotAsync(session.QuestionSetId);
            var answers = await _answerRepository.GetEntitiesBySessionIdAsync(session.Id);
            var feedbacks = await _feedbackRepository.GetBySessionIdAsync(session.Id);
            var feedbackByAnswerId = feedbacks.ToDictionary(f => f.CandidateAnswerId);

            var setDetail = await _marketplaceRepository.GetPublishedByIdAsync(session.QuestionSetId);
            var setSkills = ParseCriteriaList(setDetail?.SkillsJson);
            // Bổ sung skill distinct từ câu hỏi nếu SkillsJson trống
            if (setSkills.Count == 0)
            {
                setSkills = questions
                    .Select(q => q.Skill)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(10)
                    .ToList();
            }

            var summaries = new List<QuestionInsightSummaryDto>();
            var meaningfulAnswered = 0;
            foreach (var answer in answers)
            {
                var skipGated = AnswerEvaluationGate.TrySkip(answer.AnswerText, out _);
                if (!skipGated)
                    meaningfulAnswered++;

                if (!feedbackByAnswerId.TryGetValue(answer.Id, out var fb))
                    continue;
                if (fb.EvaluationStatus != AiFeedbackEvaluationStatus.Succeeded || fb.Score is null)
                    continue;
                // Câu trống/quá ngắn (không gọi AI) không đưa vào insight — tránh nhận xét "đa số câu quá ngắn".
                if (skipGated)
                    continue;

                var q = questions.FirstOrDefault(x => x.Id == answer.QuestionSetQuestionId);
                summaries.Add(new QuestionInsightSummaryDto
                {
                    QuestionType = q?.QuestionType,
                    Skill = q?.Skill,
                    Score = fb.Score,
                    Strengths = DeserializeStringList(fb.StrengthsJson).Take(2).ToList(),
                    Improvements = DeserializeStringList(fb.ImprovementsJson).Take(2).ToList(),
                    DimensionScores = DeserializeDimensionScores(fb.DimensionScoresJson)
                });
            }

            // Ưu tiên câu điểm thấp, tối đa 10
            summaries = summaries
                .OrderBy(s => s.Score ?? 999)
                .Take(10)
                .ToList();

            var ragResult = await _ragService.GeneratePracticeSessionInsightAsync(new PracticeSessionInsightRequest
            {
                OverallScore = session.OverallScore,
                TotalQuestions = questions.Count,
                AnsweredCount = meaningfulAnswered,
                SetTitle = setDetail?.Title,
                SetSkills = setSkills,
                QuestionSummaries = summaries
            });

            if (!ragResult.Success
                || string.IsNullOrWhiteSpace(ragResult.InsightVi)
                || string.IsNullOrWhiteSpace(ragResult.InsightEn))
            {
                _logger.LogWarning(
                    "RAG practice-session-insight thất bại cho session {SessionId}: {Error}",
                    session.Id, ragResult.Error ?? ragResult.Detail ?? "unknown");
                return;
            }

            session.AiInsightVi = ragResult.InsightVi.Trim();
            session.AiInsightEn = ragResult.InsightEn.Trim();
            session.SkillsToImproveJson = JsonSerializer.Serialize(new
            {
                vi = ragResult.SkillsToImproveVi ?? new List<string>(),
                en = ragResult.SkillsToImproveEn ?? new List<string>()
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Sinh AI Insight thất bại cho session {SessionId} — complete vẫn thành công.",
                session.Id);
        }
    }

    /// <summary>SCRUM-304 — overallScore = tổng Score Succeeded / số câu trong bộ.</summary>
    private async Task<double?> ComputeSessionOverallScoreAsync(PracticeSession session)
    {
        var questions = await _marketplaceRepository.GetQuestionsSnapshotAsync(session.QuestionSetId);
        var scores = await _feedbackRepository.GetSucceededScoresAsync(session.Id);
        return PracticeOverallScoreCalculator.Compute(scores, questions.Count);
    }

    /// <summary>Rule MVP SCRUM-291 — lỗi ở bước này không được làm fail việc nộp bài (phiên đã COMPLETED thành công).</summary>
    private async Task TryGenerateRecommendationAsync(PracticeSession session)
    {
        try
        {
            await _recommendationService.GenerateForCompletedSessionAsync(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Tạo recommendation thất bại cho session {SessionId} (candidate {CandidateUserId}).",
                session.Id, session.CandidateUserId);
        }
    }

    private async Task<PracticeSessionResponseDto> BuildSessionResponseAsync(PracticeSession session)
    {
        var questions = await _marketplaceRepository.GetQuestionsSnapshotAsync(session.QuestionSetId);
        var answers = await _answerRepository.GetAnswersBySessionIdAsync(session.Id);
        var timeLimitMinutes = await _sessionRepository.GetTimeLimitMinutesAsync(session.QuestionSetId);
        return await MapToResponseDtoAsync(session, questions, answers, timeLimitMinutes);
    }

    private static int? ComputeDurationSeconds(DateTime? startedAt, DateTime? completedAt)
    {
        if (!startedAt.HasValue || !completedAt.HasValue)
            return null;
        return (int)(completedAt.Value - startedAt.Value).TotalSeconds;
    }

    private async Task<PracticeSessionResponseDto> MapToResponseDtoAsync(
        PracticeSession session, IReadOnlyList<PublishedQuestionRow> questions,
        Dictionary<Guid, string> answersByQuestionId, int? timeLimitMinutes)
    {
        var questionDtos = new List<PracticeSessionQuestionDto>(questions.Count);
        foreach (var q in questions)
        {
            var meta = QuestionRationaleMetaParser.Parse(q.Rationale);
            questionDtos.Add(new PracticeSessionQuestionDto
            {
                Id = q.Id,
                Order = q.Order,
                Question = q.Question,
                QuestionType = q.QuestionType,
                Difficulty = q.Difficulty,
                Skill = q.Skill,
                FocusArea = q.FocusArea,
                CodeTemplateType = meta.CodeTemplateType,
                CodeSnippet = meta.CodeSnippet,
                AttachedImageUrl = await TryResolveImageUrlAsync(q.AttachedImageBlobPath),
                AnswerMethod = AnswerMethodNormalizer.Resolve(
                    q.AnswerMethod, meta.CodeTemplateType, meta.CodeSnippet),
                AnswerText = answersByQuestionId.TryGetValue(q.Id, out var text) ? text : null
            });
        }

        return new PracticeSessionResponseDto
        {
            Id = session.Id,
            QuestionSetId = session.QuestionSetId,
            Status = session.Status,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            OverallScore = session.OverallScore,
            AiInsight = MapAiInsight(session),
            TimeLimitMinutes = timeLimitMinutes,
            ExpiresAt = session.Status == PracticeSessionStatus.InProgress
                ? ComputeExpiresAt(session.StartedAt, timeLimitMinutes)
                : null,
            AntiCheatEnabled = session.AntiCheatEnabled,
            AntiCheatMaxTabLeaves = session.AntiCheatMaxTabLeaves,
            TabLeaveCount = session.TabLeaveCount,
            Questions = questionDtos
        };
    }

    private async Task<string?> TryResolveImageUrlAsync(string? blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
            return null;

        try
        {
            return await _blobStorage.GenerateReadSasUrlAsync(blobPath, TimeSpan.FromHours(2));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không tạo được SAS URL cho ảnh practice session (blob={BlobPath}).", blobPath);
            return null;
        }
    }

    private static PracticeAiInsightDto? MapAiInsight(PracticeSession session)
    {
        if (string.IsNullOrWhiteSpace(session.AiInsightVi) || string.IsNullOrWhiteSpace(session.AiInsightEn))
            return null;

        var skills = DeserializeSkillsToImprove(session.SkillsToImproveJson);
        return new PracticeAiInsightDto
        {
            Vi = session.AiInsightVi,
            En = session.AiInsightEn,
            SkillsToImprove = skills
        };
    }

    private static BilingualStringListDto DeserializeSkillsToImprove(string? json)
    {
        var empty = new BilingualStringListDto();
        if (string.IsNullOrWhiteSpace(json))
            return empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new BilingualStringListDto
            {
                Vi = ReadStringArray(root, "vi"),
                En = ReadStringArray(root, "en")
            };
        }
        catch (JsonException)
        {
            return empty;
        }
    }

    private static List<string> ReadStringArray(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return arr.EnumerateArray()
            .Select(e => e.GetString()?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList();
    }

    private static List<string> ParseCriteriaList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var list = new List<string>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        list.Add(s);
                }
                else if (el.ValueKind == JsonValueKind.Object)
                {
                    // Một số rubric lưu dạng object — lấy text/criterion nếu có
                    if (el.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                        list.Add(textProp.GetString()!);
                    else if (el.TryGetProperty("criterion", out var critProp) && critProp.ValueKind == JsonValueKind.String)
                        list.Add(critProp.GetString()!);
                    else
                        list.Add(el.GetRawText());
                }
            }
            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Dictionary<string, double>? DeserializeDimensionScores(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
