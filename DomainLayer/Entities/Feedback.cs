using DomainLayer.Constants;

namespace DomainLayer.Entities;

public class Feedback : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public string Status { get; set; } = FeedbackStatus.Pending;
}
