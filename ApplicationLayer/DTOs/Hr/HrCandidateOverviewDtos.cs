namespace ApplicationLayer.DTOs.Hr;

/// <summary>SCRUM-398: overview ứng viên cho HR — profile + stats + session trên set của HR.</summary>
public class HrCandidateOverviewDto
{
    public Guid CandidateUserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public string? SeniorityLevel { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public List<string> TechStack { get; set; } = new();
    public string? LinkedInUrl { get; set; }
    public string? GithubUrl { get; set; }

    public int TotalSessions { get; set; }
    public double? AverageScore { get; set; }
    public double? BestScore { get; set; }
    public int CurrentStreakDays { get; set; }
    public bool HasFastSession { get; set; }

    /// <summary>SCRUM-401: ngày có practice COMPLETED trong cửa sổ heatmap (chỉ ngày có hoạt động).</summary>
    public List<HrCandidateHeatmapDayDto> HeatmapDays { get; set; } = new();
    public int LongestStreakDays { get; set; }
    public int ActiveDaysInWindow { get; set; }

    public List<HrCandidateAchievementFlagDto> Achievements { get; set; } = new();
    public List<HrCandidatePracticeOnMySetItemDto> PracticeOnMySets { get; set; } = new();
}

/// <summary>SCRUM-401: bucket 1 ngày cho contribution heatmap (GitHub-style).</summary>
public class HrCandidateHeatmapDayDto
{
    /// <summary>Ngày lịch local yyyy-MM-dd.</summary>
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Minutes { get; set; }
}

public class HrCandidateAchievementFlagDto
{
    public string Id { get; set; } = string.Empty;
    public bool Earned { get; set; }
}

public class HrCandidatePracticeOnMySetItemDto
{
    public Guid SessionId { get; set; }
    public Guid QuestionSetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SessionStatus { get; set; } = string.Empty;
    public double? OverallScore { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>Projection nội bộ repository — session của candidate trên set thuộc HR.</summary>
public class HrCandidatePracticeOnMySetRow
{
    public Guid SessionId { get; set; }
    public Guid QuestionSetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SessionStatus { get; set; } = string.Empty;
    public double? OverallScore { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>SCRUM-401: raw timestamps để service aggregate heatmap theo ngày local.</summary>
public class PracticeSessionHeatmapRawRow
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}
