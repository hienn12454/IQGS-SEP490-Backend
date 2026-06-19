namespace DomainLayer.Entities;

public class QuestionGenerationJob : BaseEntity
{
    public Guid OwnerId { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public string? HrNote { get; set; }
    public string JdInputType { get; set; } = Constants.JdInputType.Text;
    public string? JdFileName { get; set; }
    public int NumberOfQuestions { get; set; }
    public string Difficulty { get; set; } = "medium";
    public string QuestionTypesJson { get; set; } = "[]";
    public string SkillsJson { get; set; } = "[]";
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }

    public QuestionGenerationPlan? Plan { get; set; }
    public ICollection<GeneratedQuestion> Questions { get; set; } = new List<GeneratedQuestion>();
}
