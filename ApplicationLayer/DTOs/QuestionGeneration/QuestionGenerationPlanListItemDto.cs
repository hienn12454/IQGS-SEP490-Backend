namespace ApplicationLayer.DTOs.QuestionGeneration;

/// <summary>Item trong danh sách plan đã tạo — dùng cho màn HR Plan History.</summary>
public class QuestionGenerationPlanListItemDto
{
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public int Question { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsPlanApproved { get; set; }
}
