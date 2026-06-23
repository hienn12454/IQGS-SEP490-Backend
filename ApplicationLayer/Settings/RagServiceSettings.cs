namespace ApplicationLayer.Settings;

public class RagServiceSettings
{
    public const string SectionName = "RagService";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public int TimeoutSeconds { get; set; } = 120;
    /// <summary>Timeout khi enqueue job async sang RAG (chỉ chờ 202).</summary>
    public int DispatchTimeoutSeconds { get; set; } = 30;
    public int EmbeddingDimension { get; set; } = 768;
}
