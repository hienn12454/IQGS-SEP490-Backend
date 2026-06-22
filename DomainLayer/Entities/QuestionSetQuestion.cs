namespace DomainLayer.Entities;

public class QuestionSetQuestion : BaseEntity
{
    public Guid QuestionSetId { get; set; }
    public int Order { get; set; }
    public string Question { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Skill { get; set; }
    public string? FocusArea { get; set; }
    public string? Rationale { get; set; }
    public string? SampleAnswer { get; set; }
    public string EvaluationCriteriaJson { get; set; } = "[]";
    public string CitationsJson { get; set; } = "[]";

    public QuestionSet QuestionSet { get; set; } = null!;
}
