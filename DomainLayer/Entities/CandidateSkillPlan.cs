namespace DomainLayer.Entities;

public class CandidateSkillPlan : BaseEntity
{
    public Guid CandidateUserId { get; set; }
    public Guid? SourceDiagnosticSetId { get; set; }
    public string Status { get; set; } = Constants.CandidateSkillPlanStatus.Active;

    public QuestionSet? SourceDiagnosticSet { get; set; }
    public ICollection<CandidateSkillPlanItem> Items { get; set; } = new List<CandidateSkillPlanItem>();
}
