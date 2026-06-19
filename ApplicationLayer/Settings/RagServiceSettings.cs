namespace ApplicationLayer.Settings;

public class RagServiceSettings
{
    public const string SectionName = "RagService";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public int TimeoutSeconds { get; set; } = 120;
    public int EmbeddingDimension { get; set; } = 768;
}
