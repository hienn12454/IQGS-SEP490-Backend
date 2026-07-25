namespace ApplicationLayer.DTOs.Hr;

/// <summary>SCRUM-324: kết quả toggle bookmark bộ câu hỏi của chính HR.</summary>
public class HrBookmarkToggleResponseDto
{
    public Guid QuestionSetId { get; set; }
    public bool Bookmarked { get; set; }
}
