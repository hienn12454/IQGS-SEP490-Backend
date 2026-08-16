using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Candidate;

public class CreateCvDrillDto
{
    [Required]
    [MaxLength(200)]
    public string Skill { get; set; } = string.Empty;
}

public class CandidateSkillPlanDto
{
    public Guid Id { get; set; }
    public Guid? SourceDiagnosticSetId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<CandidateSkillPlanItemDto> Items { get; set; } = new();
}

public class CandidateSkillPlanItemDto
{
    public Guid Id { get; set; }
    public string Skill { get; set; } = string.Empty;
    public double? BaselineScore { get; set; }
    public double? CurrentScore { get; set; }
    public double TargetScore { get; set; }
    public string Status { get; set; } = string.Empty;
}
