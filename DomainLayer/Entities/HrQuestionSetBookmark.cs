namespace DomainLayer.Entities;

/// <summary>HR bookmark bộ câu hỏi của chính mình để mở lại nhanh (SCRUM-324) — tách riêng khỏi QuestionSetBookmark (Candidate).</summary>
public class HrQuestionSetBookmark : BaseEntity
{
    public Guid HrUserId { get; set; }
    public Guid QuestionSetId { get; set; }

    public QuestionSet QuestionSet { get; set; } = null!;
}
