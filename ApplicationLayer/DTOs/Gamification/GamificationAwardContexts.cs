namespace ApplicationLayer.DTOs.Gamification;

/// <summary>
/// Input cho IGamificationService.AwardQuestionCompletionAsync — chỉ gọi SAU khi AI đã chấm điểm thành
/// công (AiFeedback.EvaluationStatus == Succeeded) và điểm đã persist ở backend (SCRUM-282/332).
/// Không bao giờ nhận Score trực tiếp từ client.
/// </summary>
public sealed class QuestionCompletionXpContext
{
    public required Guid UserId { get; init; }
    public required Guid QuestionSetQuestionId { get; init; }

    /// <summary>Id của CandidateAnswer — dùng làm khoá idempotency (1 answer chỉ trigger award đúng 1 lần).</summary>
    public required Guid CandidateAnswerId { get; init; }

    public required Guid QuestionSetId { get; init; }
    public required Guid PracticeSessionId { get; init; }

    /// <summary>Điểm AI chấm đã persist (0-100) — nguồn duy nhất được tin cậy cho ScoreBonus.</summary>
    public required double Score { get; init; }

    /// <summary>QuestionSetQuestion.QuestionType — dùng phân loại achievement Technical/System Design.</summary>
    public required string QuestionType { get; init; }

    /// <summary>Điểm Succeeded gần nhất của CÙNG câu hỏi ở 1 phiên COMPLETED khác trước đó — null nếu chưa từng làm lại câu này.</summary>
    public double? PreviousBestScore { get; init; }

    public required DateTime OccurredAtUtc { get; init; }
}

/// <summary>Input cho IGamificationService.AwardQuestionSetCompletionAsync — gọi khi PracticeSession chuyển COMPLETED.</summary>
public sealed class QuestionSetCompletionXpContext
{
    public required Guid UserId { get; init; }
    public required Guid PracticeSessionId { get; init; }
    public required Guid QuestionSetId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
}
