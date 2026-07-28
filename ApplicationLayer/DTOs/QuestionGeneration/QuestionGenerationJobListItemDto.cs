namespace ApplicationLayer.DTOs.QuestionGeneration;

public class QuestionGenerationJobListItemDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string JobDescriptionPreview { get; set; } = string.Empty;
    public int NumberOfQuestions { get; set; }
    public string JdInputType { get; set; } = string.Empty;
    public string? JdFileName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int QuestionCount { get; set; }

    /// <summary>True khi session đã được lưu thành QuestionSet draft.</summary>
    public bool HasDraft { get; set; }

    /// <summary>SCRUM-374: job mirror từ Studio v2 (HrNote bắt đầu bằng STUDIO_MIRROR).</summary>
    public bool IsFromStudio { get; set; }
}
