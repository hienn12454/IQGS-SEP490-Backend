namespace DomainLayer.Entities;

public class QuestionSetBookmark : BaseEntity
{
    public Guid CandidateUserId { get; set; }
    public Guid QuestionSetId { get; set; }

    public QuestionSet QuestionSet { get; set; } = null!;
}
