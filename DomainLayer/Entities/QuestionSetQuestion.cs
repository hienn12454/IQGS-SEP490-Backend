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
    /// <summary>SCRUM-396: path Azure Blob ảnh HR đính kèm (History / Question Set).</summary>
    public string? AttachedImageBlobPath { get; set; }
    /// <summary>SCRUM-400: Text | Code — phương thức trả lời Candidate.</summary>
    public string AnswerMethod { get; set; } = "Text";
    public string EvaluationCriteriaJson { get; set; } = "[]";
    public string CitationsJson { get; set; } = "[]";

    public QuestionSet QuestionSet { get; set; } = null!;
}
