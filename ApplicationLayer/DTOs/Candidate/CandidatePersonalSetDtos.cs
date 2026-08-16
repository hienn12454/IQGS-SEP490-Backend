using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Candidate;

public class CreatePersonalSetFromTextDto
{
    [Required]
    public string JobDescription { get; set; } = string.Empty;

    public int NumberOfQuestions { get; set; } = 10;
}

public class CandidatePersonalSetJobDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public Guid? QuestionSetId { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> CvSkills { get; set; } = new();
    public List<string> GapSkills { get; set; } = new();
    public List<string> FocusSkills { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class CandidatePersonalSetListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public int TotalQuestions { get; set; }
    public double? MyLastScore { get; set; }
    public DateTime? MyLastCompletedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}
