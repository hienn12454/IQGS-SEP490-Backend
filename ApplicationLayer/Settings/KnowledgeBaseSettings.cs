namespace ApplicationLayer.Settings;

public class KnowledgeBaseSettings
{
    public const string SectionName = "KnowledgeBase";

    public int MaxFileSizeMb { get; set; } = 50;
    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx", ".txt"];
    public int MaxConcurrentIngestJobs { get; set; } = 3;
}
