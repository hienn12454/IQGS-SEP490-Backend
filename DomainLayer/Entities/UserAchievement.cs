namespace DomainLayer.Entities;

/// <summary>Achievement đã mở khoá của 1 Candidate — code-defined rules (xem DomainLayer.Constants.AchievementCode). Unique (UserId, AchievementCode).</summary>
public class UserAchievement : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string AchievementCode { get; set; } = string.Empty;

    public DateTime UnlockedAtUtc { get; set; } = DateTime.UtcNow;
}
