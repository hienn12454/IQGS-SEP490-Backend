namespace DomainLayer.Entities;

/// <summary>Candidate đánh giá (rating + nhận xét) 1 Question Set của HR sau khi đã luyện tập — khác với Feedback (testimonial landing page).</summary>
public class QuestionSetFeedback : BaseEntity
{
    public Guid QuestionSetId { get; set; }
    public QuestionSet QuestionSet { get; set; } = null!;

    public Guid CandidateUserId { get; set; }
    public User CandidateUser { get; set; } = null!;

    /// <summary>1-5 sao.</summary>
    public int Rating { get; set; }

    public string? Comment { get; set; }
}
