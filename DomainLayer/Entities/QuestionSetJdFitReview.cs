namespace DomainLayer.Entities;

/// <summary>Bản đánh giá JD Fit mới nhất của 1 Question Set (1–1). Không lưu lịch sử.</summary>
public class QuestionSetJdFitReview : BaseEntity
{
    public Guid QuestionSetId { get; set; }
    public QuestionSet QuestionSet { get; set; } = null!;

    /// <summary>Payload qualitative (verdict, summary, flags, topics, actions) — jsonb.</summary>
    public string ReviewJson { get; set; } = "{}";

    /// <summary>SHA-256 hex của JD + câu hỏi active (id, text, type, skill, order).</summary>
    public string ContentHash { get; set; } = string.Empty;

    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
}
