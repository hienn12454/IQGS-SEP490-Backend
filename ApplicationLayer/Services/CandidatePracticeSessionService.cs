using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class CandidatePracticeSessionService : ICandidatePracticeSessionService
{
    private readonly IPracticeSessionRepository _sessionRepository;
    private readonly ICandidateMarketplaceRepository _marketplaceRepository;
    private readonly ICandidateAnswerRepository _answerRepository;

    public CandidatePracticeSessionService(
        IPracticeSessionRepository sessionRepository,
        ICandidateMarketplaceRepository marketplaceRepository,
        ICandidateAnswerRepository answerRepository)
    {
        _sessionRepository = sessionRepository;
        _marketplaceRepository = marketplaceRepository;
        _answerRepository = answerRepository;
    }

    /// <summary>Tạo phiên mới, hoặc trả về phiên IN_PROGRESS đã có cho cùng bộ câu hỏi (resume — AC-02 SCRUM-298).</summary>
    public async Task<PracticeSessionResponseDto> StartAsync(Guid questionSetId, Guid candidateUserId)
    {
        if (!await _marketplaceRepository.IsPublishedAsync(questionSetId))
            throw new NotFoundException("Bộ câu hỏi không tồn tại hoặc chưa được publish.");

        var existingSession = await _sessionRepository.GetInProgressByQuestionSetAsync(candidateUserId, questionSetId);
        if (existingSession is not null)
            return await BuildSessionResponseAsync(existingSession);

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
        return await BuildSessionResponseAsync(session);
    }

    public async Task<SubmitAnswerResponseDto> SubmitAnswerAsync(
        Guid sessionId, Guid candidateUserId, SubmitAnswerDto dto)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);

        if (session.Status != PracticeSessionStatus.InProgress)
            throw new BadRequestException("Chỉ có thể nộp câu trả lời khi phiên đang ở trạng thái IN_PROGRESS.");

        if (!await _marketplaceRepository.QuestionBelongsToSetAsync(dto.QuestionId, session.QuestionSetId))
            throw new BadRequestException("questionId không thuộc bộ câu hỏi của phiên luyện tập này.");

        var answerText = dto.AnswerText.Trim();
        var submittedAt = DateTime.UtcNow;

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
            SubmittedAt = submittedAt
        };
    }

    public async Task<PracticeSessionCompleteResponseDto> CompleteAsync(Guid sessionId, Guid candidateUserId)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);

        if (session.Status != PracticeSessionStatus.InProgress)
            throw new BadRequestException("Chỉ có thể hoàn thành phiên đang ở trạng thái IN_PROGRESS.");

        session.Status = PracticeSessionStatus.Completed;
        session.CompletedAt = DateTime.UtcNow;
        // OverallScore: chưa có bảng ai_feedbacks (SCRUM-282 chưa triển khai) — để null, tính sau khi SCRUM-282 xong.
        await _sessionRepository.UpdateAsync(session);

        return new PracticeSessionCompleteResponseDto
        {
            SessionId = session.Id,
            Status = session.Status,
            CompletedAt = session.CompletedAt,
            OverallScore = session.OverallScore,
            DurationSeconds = ComputeDurationSeconds(session.StartedAt, session.CompletedAt)
        };
    }

    /// <summary>Candidate chủ động bỏ phiên đang làm dở (MVP optional — SCRUM-298).</summary>
    public async Task<PracticeSessionResponseDto> AbandonAsync(Guid sessionId, Guid candidateUserId)
    {
        var session = await GetOwnedSessionAsync(sessionId, candidateUserId);

        if (session.Status != PracticeSessionStatus.InProgress)
            throw new BadRequestException("Chỉ có thể huỷ phiên đang ở trạng thái IN_PROGRESS.");

        session.Status = PracticeSessionStatus.Abandoned;
        await _sessionRepository.UpdateAsync(session);

        return await BuildSessionResponseAsync(session);
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

    private async Task<PracticeSession> GetOwnedSessionAsync(Guid sessionId, Guid candidateUserId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new NotFoundException("Phiên luyện tập không tồn tại.");

        if (session.CandidateUserId != candidateUserId)
            throw new ForbiddenException("Bạn không có quyền truy cập phiên luyện tập này.");

        return session;
    }

    private async Task<PracticeSessionResponseDto> BuildSessionResponseAsync(PracticeSession session)
    {
        var questions = await _marketplaceRepository.GetQuestionsSnapshotAsync(session.QuestionSetId);
        var answers = await _answerRepository.GetAnswersBySessionIdAsync(session.Id);
        return MapToResponseDto(session, questions, answers);
    }

    private static int? ComputeDurationSeconds(DateTime? startedAt, DateTime? completedAt)
    {
        if (!startedAt.HasValue || !completedAt.HasValue)
            return null;
        return (int)(completedAt.Value - startedAt.Value).TotalSeconds;
    }

    private static PracticeSessionResponseDto MapToResponseDto(
        PracticeSession session, IReadOnlyList<PublishedQuestionRow> questions, Dictionary<Guid, string> answersByQuestionId)
    {
        return new PracticeSessionResponseDto
        {
            Id = session.Id,
            QuestionSetId = session.QuestionSetId,
            Status = session.Status,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            OverallScore = session.OverallScore,
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
}
