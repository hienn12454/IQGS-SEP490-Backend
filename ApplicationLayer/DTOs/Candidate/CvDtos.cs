namespace ApplicationLayer.DTOs.Candidate;

/// <summary>Response chung cho POST (upload) và GET (trạng thái) CV — cùng schema theo SCRUM-300.</summary>
public class CvEvaluationResponseDto
{
    public string CvFileName { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public string? Summary { get; set; }
    public List<string> TechStack { get; set; } = new();
    public DateTime? ParsedAt { get; set; }
    public DateTime? UploadedAt { get; set; }
    public string? DownloadUrl { get; set; }
}
