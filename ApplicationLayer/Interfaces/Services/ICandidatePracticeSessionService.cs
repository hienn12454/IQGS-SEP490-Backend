using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.Interfaces.Services;

public interface ICandidatePracticeSessionService
{
    Task<PracticeSessionResponseDto> StartAsync(Guid questionSetId, Guid candidateUserId);
    Task<PracticeSessionResponseDto> GetByIdAsync(Guid sessionId, Guid candidateUserId);
    Task<SubmitAnswerResponseDto> SubmitAnswerAsync(Guid sessionId, Guid candidateUserId, SubmitAnswerDto dto);
    Task<PracticeSessionCompleteResponseDto> CompleteAsync(Guid sessionId, Guid candidateUserId);

    /// <summary>
    /// Watchdog hết giờ: chuyển COMPLETED, tính overallScore + AI Insight, recommendation (SCRUM-305).
    /// </summary>
    Task FinalizeExpiredByWatchdogAsync(Guid sessionId);

    Task<PracticeSessionResponseDto> AbandonAsync(Guid sessionId, Guid candidateUserId);
    Task<PracticeSessionFeedbackDto> GetFeedbackAsync(Guid sessionId, Guid candidateUserId);
    Task<PagedResultDto<PracticeSessionListItemDto>> ListAsync(Guid candidateUserId, PracticeSessionListQueryDto query);
    Task<PracticeSessionStatsDto> GetStatsAsync(Guid candidateUserId, PracticeSessionStatsQueryDto query);
    Task<IReadOnlyList<CandidateSkillStatDto>> GetSkillStatsAsync(Guid candidateUserId);

    /// <summary>SCRUM-446: ghi nhận sự kiện integrity (TAB_HIDDEN); đủ ngưỡng thì tự nộp bài.</summary>
    Task<PracticeIntegrityEventResponseDto> ReportIntegrityEventAsync(
        Guid sessionId, Guid candidateUserId, PracticeIntegrityEventDto dto);

    /// <summary>Chấm lại bài chẩn đoán Coach gần nhất từ session đã nộp.</summary>
    Task RescoreLatestCoachDiagnosticAsync(Guid candidateUserId);
}
