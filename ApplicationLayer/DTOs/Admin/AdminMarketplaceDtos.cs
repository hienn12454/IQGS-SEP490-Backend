using ApplicationLayer.DTOs.QuestionSet;

namespace ApplicationLayer.DTOs.Admin;

/// <summary>SCRUM-404: query list Marketplace phía Admin.</summary>
public class AdminMarketplaceListQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? HrUserId { get; set; }

    /// <summary>featured | newest | most_practiced | highest_rated — mặc định featured.</summary>
    public string? SortBy { get; set; }
}

public class AdminMarketplaceListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid HrUserId { get; set; }
    public string HrName { get; set; } = string.Empty;
    public string HrEmail { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogo { get; set; }
    public string Difficulty { get; set; } = "Medium";
    public List<string> Skills { get; set; } = new();
    public int TotalQuestions { get; set; }
    public int EstimatedTimeMinutes { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public int AttemptCount { get; set; }
    public int UniqueCandidateCount { get; set; }
    public double? Rating { get; set; }
    public bool IsPinned { get; set; }
    public bool IsTrending { get; set; }
    public DateTime? PinnedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class AdminMarketplaceDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid HrUserId { get; set; }
    public string HrName { get; set; } = string.Empty;
    public string HrEmail { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogo { get; set; }
    public int TotalQuestions { get; set; }
    public int AttemptCount { get; set; }
    public int UniqueCandidateCount { get; set; }
    public double? Rating { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? PinnedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public List<AdminMarketplaceQuestionSummaryDto> Questions { get; set; } = new();
    public List<QuestionSetPractitionerDto> Practitioners { get; set; } = new();
}

public class AdminMarketplaceQuestionSummaryDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string Question { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Skill { get; set; }
}

public class AdminMarketplacePinResultDto
{
    public Guid QuestionSetId { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? PinnedAt { get; set; }
}

public class AdminMarketplaceStatsDto
{
    public int TotalPublished { get; set; }
    public int PracticesLast7Days { get; set; }
    public int PinnedCount { get; set; }
    public List<AdminMarketplaceTopHrDto> TopHrs { get; set; } = new();
    public List<AdminMarketplaceTopSkillDto> TopSkills { get; set; } = new();
}

public class AdminMarketplaceTopHrDto
{
    public Guid HrUserId { get; set; }
    public string HrName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int PublishedSetCount { get; set; }
    public int AttemptCount { get; set; }
}

public class AdminMarketplaceTopSkillDto
{
    public string Skill { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
}

/// <summary>Read-model nội bộ repo Admin Marketplace.</summary>
public class AdminMarketplaceSetRow
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public Guid HrUserId { get; set; }
    public string HrName { get; set; } = string.Empty;
    public string HrEmail { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogo { get; set; }
    public string? CompanyWebsite { get; set; }
    public string Difficulty { get; set; } = "medium";
    public List<string> Skills { get; set; } = new();
    public int TotalQuestions { get; set; }
    public int AttemptCount { get; set; }
    public int UniqueCandidateCount { get; set; }
    public double? Rating { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? PinnedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int? TimeLimitMinutes { get; set; }
}
