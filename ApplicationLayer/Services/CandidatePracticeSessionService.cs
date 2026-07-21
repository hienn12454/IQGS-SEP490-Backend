using System.Text.Json;
using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<CandidatePracticeSessionService> _logger;

    public CandidatePracticeSessionService(
        IPracticeSessionRepository sessionRepository,
        ICandidateMarketplaceRepository marketplaceRepository,
        ICandidateAnswerRepository answerRepository,
        IAiFeedbackRepository feedbackRepository,
        IRagService ragService,
        IRecommendationService recommendationService,
        ILogger<CandidatePracticeSessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _marketplaceRepository = marketplaceRepository;
        _answerRepository = answerRepository;
        _feedbackRepository = feedbackRepository;
        _ragService = ragService;
        _recommendationService = recommendationService;
        _logger = logger;
    }

    /// <summary>Tạo phiên mới, hoặc trả về phiên IN_PROGRESS đã có cho cùng bộ câu hỏi (resume — AC-02 SCRUM-298).</summary>
    public async Task<PracticeSessionResponseDto> StartAsync(Guid questionSetId, Guid candidateUserId)
    {
        if (!await _marketplaceRepository.IsPublishedAsync(questionSetId))
            throw new NotFoundException("Bộ câu hỏi không tồn tại hoặc chưa được publish.");

        var existingSession = await _sessionRepository.GetInProgressByQuestionSetAsync(candidateUserId, questionSetId);
        if (existingSession is not null)
        {
            var timeLimit = await _sessionRepository.GetTimeLimitMinutesAsync(questionSetId);
            if (!await AutoSubmitIfExpiredAsync(existingSession, timeLimit))
                return await BuildSessionResponseAsync(existingSession);
            // Phiên dở dang đã hết giờ và vừa được tự động nộp — bắt đầu phiên mới bên dưới.
        }

        var session = new PracticeSession
        {
            CandidateUserId = candidateUserId,
            QuestionSetId = questionSetId,
            Status = PracticeSessionStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };
        await _sessionRepository.AddAsync(session);

        return await BuildSessionResponseAsync(session);
    }

    public async Task<PracticeSessionResponseDto> GetByIdAsync(Guid sessionId, Guid candidateUserId)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);
        await AutoSubmitIfExpiredAsync(session, await _sessionRepository.GetTimeLimitMinutesAsync(session.QuestionSetId));
        return await BuildSessionResponseAsync(session);
    }

    public async Task<SubmitAnswerResponseDto> SubmitAnswerAsync(
        Guid sessionId, Guid candidateUserId, SubmitAnswerDto dto)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);

        if (await AutoSubmitIfExpiredAsync(session, await _sessionRepository.GetTimeLimitMinutesAsync(session.QuestionSetId)))
            throw new BadRequestException("Đã hết thời gian làm bài — phiên đã được tự động nộp.");

        if (session.Status != PracticeSessionStatus.InProgress)
            throw new BadRequestException("Chỉ có thể nộp câu trả lời khi phiên đang ở trạng thái IN_PROGRESS.");

        if (!await _marketplaceRepository.QuestionBelongsToSetAsync(dto.QuestionId, session.QuestionSetId))
            throw new BadRequestException("questionId không thuộc bộ câu hỏi của phiên luyện tập này.");

        var answerText = dto.AnswerText.Trim();
        var submittedAt = DateTime.UtcNow;

        // 1) Luôn lưu answer trước — AC-03: RAG fail vẫn giữ câu trả lời
        var existing = await _answerRepository.GetAsync(sessionId, dto.QuestionId);
        CandidateAnswer answer;
        if (existing is null)
        {
            answer = new CandidateAnswer
            {
                PracticeSessionId = sessionId,
                QuestionSetQuestionId = dto.QuestionId,
                AnswerText = answerText,
                SubmittedAt = submittedAt
            };
            await _answerRepository.AddAsync(answer);
        }
        else
        {
            existing.AnswerText = answerText;
            existing.SubmittedAt = submittedAt;
            await _answerRepository.UpdateAsync(existing);
            answer = existing;
        }

        // 2) Gate: câu trống / "không biết" / spam → score 0 Succeeded, không gọi RAG (tiết kiệm token)
        if (AnswerEvaluationGate.TrySkip(answerText, out var skipReason))
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

            return new SubmitAnswerResponseDto
            {
                QuestionId = dto.QuestionId,
                AnswerText = answerText,
                SubmittedAt = submittedAt,
                EvaluationStatus = AiFeedbackEvaluationStatus.Succeeded,
                Score = 0,
                Strengths = [],
                Improvements = [skipReason],
                Suggestion = skipReason,
                DimensionScores = null,
                EvaluationError = null
            };
        }

        // 3) Gọi RAG evaluate sync (SCRUM-282) — lỗi → lưu Failed, không throw ra FE
        var evaluation = await EvaluateAndPersistAsync(answer, dto.QuestionId);

        return new SubmitAnswerResponseDto
        {
            QuestionId = dto.QuestionId,
            AnswerText = answerText,
            SubmittedAt = submittedAt,
            EvaluationStatus = evaluation.EvaluationStatus,
            Score = evaluation.Score,
            Strengths = evaluation.Strengths,
            Improvements = evaluation.Improvements,
            Suggestion = evaluation.Suggestion,
            DimensionScores = evaluation.DimensionScores,
            EvaluationError = evaluation.ErrorMessage
        };
    }

    public async Task<PracticeSessionCompleteResponseDto> CompleteAsync(Guid sessionId, Guid candidateUserId)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);

        // Hết giờ thì phiên đã được tự nộp tại đúng deadline — candidate bấm nộp sau đó vẫn nhận kết quả bình thường.
        if (!await AutoSubmitIfExpiredAsync(session, await _sessionRepository.GetTimeLimitMinutesAsync(session.QuestionSetId)))
        {
            if (session.Status != PracticeSessionStatus.InProgress)
                throw new BadRequestException("Chỉ có thể hoàn thành phiên đang ở trạng thái IN_PROGRESS.");

            session.Status = PracticeSessionStatus.Completed;
            session.CompletedAt = DateTime.UtcNow;
            await FinalizeCompletedSessionAsync(session);
        }

        return new PracticeSessionCompleteResponseDto
        {
            SessionId = session.Id,
            Status = session.Status,
            CompletedAt = session.CompletedAt,
            OverallScore = session.OverallScore,
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

        if (await AutoSubmitIfExpiredAsync(session, await _sessionRepository.GetTimeLimitMinutesAsync(session.QuestionSetId)))
            throw new BadRequestException("Phiên đã hết thời gian và được tự động nộp — không thể huỷ.");

        if (session.Status != PracticeSessionStatus.InProgress)
            throw new BadRequestException("Chỉ có thể huỷ phiên đang ở trạng thái IN_PROGRESS.");

        session.Status = PracticeSessionStatus.Abandoned;
        await _sessionRepository.UpdateAsync(session);

        return await BuildSessionResponseAsync(session);
    }

    public async Task<PracticeSessionFeedbackDto> GetFeedbackAsync(Guid sessionId, Guid candidateUserId)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);
        var questions = await _marketplaceRepository.GetQuestionsSnapshotAsync(session.QuestionSetId);
        var answers = await _answerRepository.GetEntitiesBySessionIdAsync(sessionId);
        var feedbacks = await _feedbackRepository.GetBySessionIdAsync(sessionId);

        var feedbackByAnswerId = feedbacks.ToDictionary(f => f.CandidateAnswerId);
        var answerByQuestionId = answers.ToDictionary(a => a.QuestionSetQuestionId);

        var items = new List<PracticeSessionFeedbackItemDto>();
        foreach (var q in questions.OrderBy(x => x.Order))
        {
            if (!answerByQuestionId.TryGetValue(q.Id, out var answer))
                continue;

            feedbackByAnswerId.TryGetValue(answer.Id, out var feedback);

            items.Add(new PracticeSessionFeedbackItemDto
            {
                QuestionId = q.Id,
                QuestionText = q.Question,
                QuestionType = q.QuestionType,
                Difficulty = q.Difficulty,
                AnswerText = answer.AnswerText,
                Score = feedback?.Score,
                Strengths = DeserializeStringList(feedback?.StrengthsJson),
                Improvements = DeserializeStringList(feedback?.ImprovementsJson),
                Suggestion = feedback?.Suggestion,
                DimensionScores = DeserializeDimensionScores(feedback?.DimensionScoresJson),
                EvaluationStatus = feedback?.EvaluationStatus ?? AiFeedbackEvaluationStatus.Failed
            });
        }

        double? overall = session.OverallScore;
        if (overall is null)
        {
            // Fallback (phiên chưa complete): cùng công thức SCRUM-304
            var succeededScores = items
                .Where(i =>
                    i.EvaluationStatus == AiFeedbackEvaluationStatus.Succeeded
                    && i.Score.HasValue)
                .Select(i => i.Score!.Value);
            overall = PracticeOverallScoreCalculator.Compute(succeededScores, questions.Count);
        }

        return new PracticeSessionFeedbackDto
        {
            SessionId = session.Id,
            OverallScore = overall,
            Status = session.Status,
            AiInsight = MapAiInsight(session),
            Items = items
        };
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

    private async Task<(
        string EvaluationStatus,
        double? Score,
        List<string> Strengths,
        List<string> Improvements,
        string? Suggestion,
        Dictionary<string, double>? DimensionScores,
        string? ErrorMessage)> EvaluateAndPersistAsync(CandidateAnswer answer, Guid questionId)
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
            var ragResult = await _ragService.EvaluateAnswerAsync(new EvaluateAnswerRequest
            {
                Question = rubric.Question,
                EvaluationCriteria = criteria,
                CandidateAnswer = answer.AnswerText,
                SampleAnswer = rubric.SampleAnswer,
                Skill = rubric.Skill,
                QuestionType = rubric.QuestionType
            });

            if (!ragResult.Success || ragResult.Score is null)
            {
                var err = ragResult.Error ?? ragResult.Detail ?? "RAG evaluate thất bại.";
                await UpsertFeedbackAsync(answer.Id, AiFeedbackEvaluationStatus.Failed, null, [], [], null, null, err);
                return (AiFeedbackEvaluationStatus.Failed, null, [], [], null, null, err);
            }

            var strengths = ragResult.Strengths ?? [];
            var improvements = ragResult.Improvements ?? [];
            await UpsertFeedbackAsync(
                answer.Id,
                AiFeedbackEvaluationStatus.Succeeded,
                ragResult.Score,
                strengths,
                improvements,
                ragResult.Suggestion,
                ragResult.DimensionScores,
                null);

            return (
                AiFeedbackEvaluationStatus.Succeeded,
                ragResult.Score,
                strengths,
                improvements,
                ragResult.Suggestion,
                ragResult.DimensionScores,
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

    /// <summary>Hạn chót nộp bài = StartedAt + giới hạn phút — null nếu bộ không giới hạn thời gian.</summary>
    private static DateTime? ComputeExpiresAt(DateTime? startedAt, int? timeLimitMinutes)
        => startedAt.HasValue && timeLimitMinutes.HasValue
            ? startedAt.Value.AddMinutes(timeLimitMinutes.Value)
            : null;

    /// <summary>
    /// Hết giờ làm bài → tự động nộp: chuyển COMPLETED với CompletedAt = đúng deadline (không tính thời gian trễ),
    /// giữ nguyên các câu trả lời đã submit và chấm điểm như nộp tay. Trả về true nếu vừa tự nộp.
    /// </summary>
    private async Task<bool> AutoSubmitIfExpiredAsync(PracticeSession session, int? timeLimitMinutes)
    {
        if (session.Status != PracticeSessionStatus.InProgress)
            return false;

        var expiresAt = ComputeExpiresAt(session.StartedAt, timeLimitMinutes);
        if (expiresAt is null || DateTime.UtcNow < expiresAt)
            return false;

        session.Status = PracticeSessionStatus.Completed;
        session.CompletedAt = expiresAt;
        session.UpdatedAt = DateTime.UtcNow;
        await FinalizeCompletedSessionAsync(session);
        return true;
    }

    /// <summary>
    /// SCRUM-304 + SCRUM-305: tính overallScore, sinh AI Insight (lỗi RAG không fail complete), recommendation.
    /// </summary>
    private async Task FinalizeCompletedSessionAsync(PracticeSession session)
    {
        session.OverallScore = await ComputeSessionOverallScoreAsync(session);
        await TryGenerateAiInsightAsync(session);
        await _sessionRepository.UpdateAsync(session);
        await TryGenerateRecommendationAsync(session);
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
            foreach (var answer in answers)
            {
                if (!feedbackByAnswerId.TryGetValue(answer.Id, out var fb))
                    continue;
                if (fb.EvaluationStatus != AiFeedbackEvaluationStatus.Succeeded || fb.Score is null)
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
                AnsweredCount = answers.Count,
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
        return MapToResponseDto(session, questions, answers, timeLimitMinutes);
    }

    private static int? ComputeDurationSeconds(DateTime? startedAt, DateTime? completedAt)
    {
        if (!startedAt.HasValue || !completedAt.HasValue)
            return null;
        return (int)(completedAt.Value - startedAt.Value).TotalSeconds;
    }

    private static PracticeSessionResponseDto MapToResponseDto(
        PracticeSession session, IReadOnlyList<PublishedQuestionRow> questions,
        Dictionary<Guid, string> answersByQuestionId, int? timeLimitMinutes)
    {
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
            Questions = questions.Select(q => new PracticeSessionQuestionDto
            {
                Id = q.Id,
                Order = q.Order,
                Question = q.Question,
                QuestionType = q.QuestionType,
                Difficulty = q.Difficulty,
                Skill = q.Skill,
                FocusArea = q.FocusArea,
                AnswerText = answersByQuestionId.TryGetValue(q.Id, out var text) ? text : null
            }).ToList()
        };
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
