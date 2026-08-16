namespace DomainLayer.Entities;

public class CandidateSkillPlanItem : BaseEntity
{
    public Guid PlanId { get; set; }
    public string Skill { get; set; } = string.Empty;
    public double? BaselineScore { get; set; }
    public double? CurrentScore { get; set; }
    public double TargetScore { get; set; } = 70;
    public string Status { get; set; } = Constants.CandidateSkillPlanItemStatus.Pending;
    public Guid? LastSessionId { get; set; }

    public CandidateSkillPlan Plan { get; set; } = null!;
    public PracticeSession? LastSession { get; set; }
}
