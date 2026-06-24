namespace ApplicationLayer.DTOs.QuestionGeneration;

/// <summary>Input gốc HR khi tạo session — mirror CreatePlanJobRequestDto + metadata upload.</summary>
public class PlanHrInputDto
{
    public string JobDescription { get; set; } = string.Empty;
    public string? HrNote { get; set; }
    public int NumberOfQuestions { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public List<string> QuestionTypes { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public string JdInputType { get; set; } = string.Empty;
    public string? JdFileName { get; set; }
}
