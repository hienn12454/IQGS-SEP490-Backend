using ApplicationLayer.DTOs.Rag;

namespace ApplicationLayer.DTOs.QuestionGeneration;

/// <summary>Payload RAG gửi qua PATCH generation-result.</summary>
public class QuestionGenerationCallbackDto
{
    public string Phase { get; set; } = string.Empty;
    public bool Success { get; set; }
    public object? Plan { get; set; }
    public List<RagGeneratedQuestionDto>? Questions { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? Source { get; set; }
    public List<string>? Errors { get; set; }
    public double? ProcessingTimeMs { get; set; }
}
