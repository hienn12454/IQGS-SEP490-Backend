using ApplicationLayer.Helpers;
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
/// CompletedAt ghi đúng deadline (StartedAt + limit); OverallScore tính cùng công thức SCRUM-304 như nộp tay.
/// </summary>
public class ExpiredPracticeSessionWatchdogJob : IExpiredPracticeSessionWatchdogJob
{
    private readonly IPracticeSessionRepository _sessionRepository;
    private readonly ICandidateMarketplaceRepository _marketplaceRepository;
    private readonly IAiFeedbackRepository _feedbackRepository;
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<ExpiredPracticeSessionWatchdogJob> _logger;

    public ExpiredPracticeSessionWatchdogJob(
        IPracticeSessionRepository sessionRepository,
        ICandidateMarketplaceRepository marketplaceRepository,
        IAiFeedbackRepository feedbackRepository,
        IRecommendationService recommendationService,
        ILogger<ExpiredPracticeSessionWatchdogJob> logger)
    {
        _sessionRepository = sessionRepository;
        _marketplaceRepository = marketplaceRepository;
        _feedbackRepository = feedbackRepository;
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

            // SCRUM-304: overall = tổng điểm Succeeded / tổng số câu trong bộ — giống hệt nộp tay/auto-submit lazy.
            var questions = await _marketplaceRepository.GetQuestionsSnapshotAsync(session.QuestionSetId);
            var scores = await _feedbackRepository.GetSucceededScoresAsync(session.Id);

            session.Status = PracticeSessionStatus.Completed;
            session.CompletedAt = expiresAt;
            session.UpdatedAt = now;
            session.OverallScore = PracticeOverallScoreCalculator.Compute(scores, questions.Count);
            await _sessionRepository.UpdateAsync(session);

            _logger.LogInformation(
                "Watchdog tự nộp phiên practice {SessionId} (candidate {CandidateUserId}) — hết giờ lúc {ExpiresAt:O}, overallScore={OverallScore}.",
                session.Id, session.CandidateUserId, expiresAt, session.OverallScore);

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
