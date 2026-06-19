namespace DomainLayer.Common;

public class StructuredErrorPayload
{
    public string Error { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string Source { get; set; } = "BE";
    public List<string> Errors { get; set; } = new();
}
