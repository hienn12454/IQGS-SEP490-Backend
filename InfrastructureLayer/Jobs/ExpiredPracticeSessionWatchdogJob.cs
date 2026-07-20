using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Jobs;

/// <summary>
/// Quét các phiên practice IN_PROGRESS thuộc bộ có giới hạn thời gian và tự nộp bài khi hết giờ —
/// bọc case candidate thoát app/mất mạng nên không còn request nào chạm vào phiên để trigger auto-submit lazy.
/// CompletedAt ghi đúng deadline (StartedAt + limit), không tính thời gian trễ do watchdog quét theo chu kỳ.
/// </summary>
public class ExpiredPracticeSessionWatchdogJob : IExpiredPracticeSessionWatchdogJob
{
    private readonly IPracticeSessionRepository _sessionRepository;
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<ExpiredPracticeSessionWatchdogJob> _logger;

    public ExpiredPracticeSessionWatchdogJob(
        IPracticeSessionRepository sessionRepository,
        IRecommendationService recommendationService,
        ILogger<ExpiredPracticeSessionWatchdogJob> logger)
    {
        _sessionRepository = sessionRepository;
        _recommendationService = recommendationService;
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

            session.Status = PracticeSessionStatus.Completed;
            session.CompletedAt = expiresAt;
            session.UpdatedAt = now;
            await _sessionRepository.UpdateAsync(session);

            _logger.LogInformation(
                "Watchdog tự nộp phiên practice {SessionId} (candidate {CandidateUserId}) — hết giờ lúc {ExpiresAt:O}.",
                session.Id, session.CandidateUserId, expiresAt);

            try
            {
                await _recommendationService.GenerateForCompletedSessionAsync(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tạo recommendation thất bại cho session {SessionId}.", session.Id);
            }
        }
    }
}
