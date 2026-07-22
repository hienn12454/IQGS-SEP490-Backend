using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Jobs;

/// <summary>
/// Quét các phiên practice IN_PROGRESS thuộc bộ có giới hạn thời gian và tự nộp bài khi hết giờ —
/// bọc case candidate thoát app/mất mạng nên không còn request nào chạm vào phiên để trigger auto-submit lazy.
/// CompletedAt ghi đúng deadline (StartedAt + limit); overallScore + AI Insight
/// được xử lý tập trung qua FinalizeExpiredByWatchdogAsync (SCRUM-304/SCRUM-305).
/// </summary>
public class ExpiredPracticeSessionWatchdogJob : IExpiredPracticeSessionWatchdogJob
{
    private readonly IPracticeSessionRepository _sessionRepository;
    private readonly ICandidatePracticeSessionService _practiceSessionService;
    private readonly ILogger<ExpiredPracticeSessionWatchdogJob> _logger;

    public ExpiredPracticeSessionWatchdogJob(
        IPracticeSessionRepository sessionRepository,
        ICandidatePracticeSessionService practiceSessionService,
        ILogger<ExpiredPracticeSessionWatchdogJob> logger)
    {
        _sessionRepository = sessionRepository;
        _practiceSessionService = practiceSessionService;
        _logger = logger;
    }

    [Queue("default")]
    public async Task ExecuteAsync()
    {
        var sessions = await _sessionRepository.GetInProgressWithTimeLimitAsync();
        var now = DateTime.UtcNow;

        foreach (var session in sessions)
        {
            var expiresAt = session.StartedAt!.Value.AddMinutes(session.QuestionSet.TimeLimitMinutes!.Value);
            if (now < expiresAt)
                continue;

            try
            {
                await _practiceSessionService.FinalizeExpiredByWatchdogAsync(session.Id);
                _logger.LogInformation(
                    "Watchdog tự nộp phiên practice {SessionId} (candidate {CandidateUserId}) — hết giờ lúc {ExpiresAt:O}.",
                    session.Id, session.CandidateUserId, expiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watchdog finalize thất bại cho session {SessionId}.", session.Id);
            }
        }
    }
}
