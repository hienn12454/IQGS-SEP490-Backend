namespace ApplicationLayer.DTOs.Recommendation;

public class SkillScoreDto
{
    public string Skill { get; set; } = string.Empty;
    public double AvgScore { get; set; }
    public int QuestionCount { get; set; }
}

public class HrRecommendationCompareItemDto
{
    public Guid Id { get; set; }
    public Guid CandidateUserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public double OverallScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public double FitPercent { get; set; }
    public List<string> CvSkills { get; set; } = new();
    public List<string> JdSkills { get; set; } = new();
    public List<string> MatchedSkills { get; set; } = new();
    public List<string> MissingOnCv { get; set; } = new();
    public List<SkillScoreDto> SkillScores { get; set; } = new();
    public string? InvitationStatus { get; set; }
    public string? LatestOfferStatus { get; set; }
    public DateTime? ViewedAt { get; set; }
}

public class HrRecommendationCompareResponseDto
{
    public Guid QuestionSetId { get; set; }
    public string QuestionSetTitle { get; set; } = string.Empty;
    public List<HrRecommendationCompareItemDto> Items { get; set; } = new();
}
