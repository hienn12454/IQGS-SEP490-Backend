namespace ApplicationLayer.Settings;

public class KnowledgeBaseSettings
{
    public const string SectionName = "KnowledgeBase";

    public int MaxFileSizeMb { get; set; } = 50;
    /// <summary>HR Knowledge — PDF/DOCX/TXT only.</summary>
    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx", ".txt"];
    /// <summary>SCRUM-448: Admin SYSTEM Knowledge — thêm .jsonl (Q/A dataset).</summary>
    public string[] AdminAllowedExtensions { get; set; } = [".pdf", ".docx", ".txt", ".jsonl"];
    public int MaxConcurrentIngestJobs { get; set; } = 3;

    /// <summary>SCRUM-448: SYSTEM dùng AdminAllowedExtensions; HR dùng AllowedExtensions.</summary>
    public string[] GetAllowedExtensionsForScope(string scope)
    {
        if (string.Equals(scope, "SYSTEM", StringComparison.OrdinalIgnoreCase))
            return AdminAllowedExtensions;
        return AllowedExtensions;
    }
}
