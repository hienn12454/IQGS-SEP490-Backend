namespace DomainLayer.Entities;

public class CandidateAnswer : BaseEntity
{
    public Guid PracticeSessionId { get; set; }
    public Guid QuestionSetQuestionId { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }

    public PracticeSession PracticeSession { get; set; } = null!;
    public QuestionSetQuestion QuestionSetQuestion { get; set; } = null!;
}
