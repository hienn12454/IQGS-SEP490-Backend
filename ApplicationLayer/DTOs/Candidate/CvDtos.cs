namespace ApplicationLayer.DTOs.Candidate;

public class CvUploadResponseDto
{
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public class CvDownloadResponseDto
{
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
