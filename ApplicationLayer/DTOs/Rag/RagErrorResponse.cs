namespace ApplicationLayer.DTOs.Rag;

/// <summary>JSON lỗi từ RAG internal API (api/exception_handlers.py).</summary>
public class RagErrorResponse
{
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string>? Errors { get; set; }
}
