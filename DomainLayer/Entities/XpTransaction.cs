namespace DomainLayer.Entities;

/// <summary>
/// Bản ghi audit bất biến cho 1 lần cộng/trừ XP — nguồn sự thật cho lịch sử XP.
/// IdempotencyKey có UNIQUE index ở DB — đảm bảo cùng 1 sự kiện (vd cùng attemptId) không bao giờ
/// được cộng XP 2 lần kể cả khi request bị retry/race.
/// </summary>
public class XpTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int Amount { get; set; }

    /// <summary>Xem DomainLayer.Constants.XpTransactionType.</summary>
    public string Type { get; set; } = string.Empty;

    public Guid? QuestionId { get; set; }
    public Guid? QuestionAttemptId { get; set; }
    public Guid? QuestionSetId { get; set; }
    public Guid? PracticeSessionId { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Khoá idempotency — vd "question-completed:{answerId}", "streak:{userId}:{localDate}:{milestone}".</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
