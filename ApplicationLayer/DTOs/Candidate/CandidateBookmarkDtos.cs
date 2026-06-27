namespace ApplicationLayer.DTOs.Candidate;

public class BookmarkToggleResponseDto
{
    public Guid QuestionSetId { get; set; }
    public bool Bookmarked { get; set; }
}
