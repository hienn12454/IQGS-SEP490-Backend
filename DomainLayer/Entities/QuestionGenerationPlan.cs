namespace DomainLayer.Entities;

public class QuestionGenerationPlan : BaseEntity
{
    public Guid JobId { get; set; }
    public string PlanJson { get; set; } = "{}";
    public bool IsApproved { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public QuestionGenerationJob Job { get; set; } = null!;
}
