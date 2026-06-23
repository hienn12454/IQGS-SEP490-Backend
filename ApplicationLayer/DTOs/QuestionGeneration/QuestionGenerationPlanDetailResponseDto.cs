namespace ApplicationLayer.DTOs.QuestionGeneration;

public class QuestionGenerationPlanDetailResponseDto
{
    public Guid PlanId { get; set; }
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsPlanApproved { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public PlanHrInputDto Input { get; set; } = new();
    public object? Plan { get; set; }
    public JobUiStateDto Ui { get; set; } = new();
}
