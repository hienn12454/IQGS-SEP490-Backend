namespace DomainLayer.Entities;

public class QuestionSet : BaseEntity
{
    public Guid OwnerId { get; set; }
    public Guid SourceJobId { get; set; }
    public string Status { get; set; } = Constants.QuestionSetStatus.Draft;
    public string? Title { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public string? HrNote { get; set; }
    public string PlanJson { get; set; } = "{}";
    public DateTime? GeneratedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public QuestionGenerationJob SourceJob { get; set; } = null!;
    public ICollection<QuestionSetQuestion> Questions { get; set; } = new List<QuestionSetQuestion>();
}
