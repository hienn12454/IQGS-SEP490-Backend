namespace ApplicationLayer.DTOs.Gamification;

/// <summary>Trạng thái gamification hiện tại của candidate — GET /api/candidate/gamification/progress.</summary>
public class UserProgressDto
{
    public long TotalXp { get; set; }
    public int Level { get; set; }

    /// <summary>XP đã tích luỹ trong level hiện tại.</summary>
    public long CurrentLevelXp { get; set; }

    /// <summary>XP cần thêm để lên level kế tiếp.</summary>
    public long XpRequiredForNextLevel { get; set; }

    /// <summary>Phần trăm hoàn thành level hiện tại (0-100).</summary>
    public decimal ProgressPercentage { get; set; }

    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }

    public int DailyGoalXp { get; set; }
    public int TodayXp { get; set; }
    public bool DailyGoalCompleted { get; set; }

    public int TotalPracticeSessions { get; set; }
}

/// <summary>Kết quả 1 lần award XP — trả kèm response submit/complete practice session.</summary>
public class XpRewardDto
{
    public int TotalEarned { get; set; }
    public List<XpRewardItemDto> Rewards { get; set; } = new();

    public int PreviousLevel { get; set; }
    public int CurrentLevel { get; set; }

    public long PreviousTotalXp { get; set; }
    public long CurrentTotalXp { get; set; }

    public bool LevelUp { get; set; }

    /// <summary>Achievement vừa mở khoá trong lần award này (rỗng nếu không có).</summary>
    public List<string> UnlockedAchievementCodes { get; set; } = new();

    public UserProgressDto Progress { get; set; } = new();
}

public class XpRewardItemDto
{
    /// <summary>Xem DomainLayer.Constants.XpTransactionType.</summary>
    public string Type { get; set; } = string.Empty;

    public int Xp { get; set; }

    /// <summary>Key i18n cho FE — vd "gamification.questionCompleted" (không trả text tiếng Việt cứng).</summary>
    public string LabelKey { get; set; } = string.Empty;
}

/// <summary>1 ngày trên activity heatmap.</summary>
public class DailyActivityDto
{
    public DateOnly Date { get; set; }
    public int Xp { get; set; }
    public int QuestionsCompleted { get; set; }
    public int SessionsCompleted { get; set; }
    public bool Active { get; set; }
}

public class ActivityQueryDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class XpHistoryItemDto
{
    public Guid Id { get; set; }
    public int Amount { get; set; }

    /// <summary>Xem DomainLayer.Constants.XpTransactionType.</summary>
    public string Type { get; set; } = string.Empty;

    public string DescriptionKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class XpHistoryQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AchievementDto
{
    /// <summary>Xem DomainLayer.Constants.AchievementCode.</summary>
    public string Code { get; set; } = string.Empty;

    public bool Unlocked { get; set; }
    public DateTime? UnlockedAtUtc { get; set; }
}

public class UpdateDailyGoalDto
{
    public int DailyGoalXp { get; set; }
}

public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
