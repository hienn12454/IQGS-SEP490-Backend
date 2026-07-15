namespace DomainLayer.Entities;

public class PracticeSession : BaseEntity
{
    public Guid CandidateUserId { get; set; }
    public Guid QuestionSetId { get; set; }
    public string Status { get; set; } = Constants.PracticeSessionStatus.InProgress;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? OverallScore { get; set; }

    public QuestionSet QuestionSet { get; set; } = null!;
}
