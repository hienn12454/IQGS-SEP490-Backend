using System.Text.Json.Serialization;

namespace ApplicationLayer.DTOs.Rag;

public class RagHealthCheckItemDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class RagHealthTechnicalDto
{
    public string RagStatus { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Config { get; set; } = string.Empty;
}

public class RagHealthStatusDto
{
    public bool IsHealthy { get; set; }
    public string Summary { get; set; } = string.Empty;

    [JsonIgnore]
    public string WrapperMessage { get; set; } = string.Empty;

    public List<RagHealthCheckItemDto> Checks { get; set; } = new();
    public string ServiceUrl { get; set; } = string.Empty;
    public long? ResponseTimeMs { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public RagHealthTechnicalDto? Technical { get; set; }
}
