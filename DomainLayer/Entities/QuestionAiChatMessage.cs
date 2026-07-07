namespace DomainLayer.Entities;

/// <summary>
/// Lưu lịch sử chat Ask AI theo từng generated question (SCRUM-215).
/// </summary>
public class QuestionAiChatMessage : BaseEntity
{
    public Guid JobId { get; set; }
    public Guid QuestionId { get; set; }
    /// <summary>hr | ai</summary>
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    /// <summary>JSON structured suggestion — chỉ message AI khi có gợi ý chỉnh sửa câu hỏi.</summary>
    public string? SuggestionJson { get; set; }

    public QuestionGenerationJob? Job { get; set; }
    public GeneratedQuestion? Question { get; set; }
}
