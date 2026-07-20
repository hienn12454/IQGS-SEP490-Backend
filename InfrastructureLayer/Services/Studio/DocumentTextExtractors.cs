using ApplicationLayer.Studio.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace InfrastructureLayer.Services.Studio;

public sealed class PdfTextExtractor : IDocumentTextExtractor
{
    public bool CanHandle(string contentType, string fileName)
        => contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
           || fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractTextAsync(byte[] content, CancellationToken ct)
    {
        using var stream = new MemoryStream(content);
        using var doc = PdfDocument.Open(stream);
        var text = string.Join("\n", doc.GetPages().Select(p => p.Text));
        return Task.FromResult(text);
    }
}

public sealed class DocxTextExtractor : IDocumentTextExtractor
{
    public bool CanHandle(string contentType, string fileName)
        => contentType.Contains("word", StringComparison.OrdinalIgnoreCase)
           || fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractTextAsync(byte[] content, CancellationToken ct)
    {
        using var stream = new MemoryStream(content);
        using var doc = WordprocessingDocument.Open(stream, false);
        var text = doc.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
        return Task.FromResult(text);
    }
}

public sealed class TxtTextExtractor : IDocumentTextExtractor
{
    public bool CanHandle(string contentType, string fileName)
        => contentType.Contains("text", StringComparison.OrdinalIgnoreCase)
           || fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractTextAsync(byte[] content, CancellationToken ct)
        => Task.FromResult(System.Text.Encoding.UTF8.GetString(content));
}

public sealed class DocumentTextExtractorFactory(IEnumerable<IDocumentTextExtractor> extractors) : IDocumentTextExtractorFactory
{
    public IDocumentTextExtractor Resolve(string contentType, string fileName)
    {
        var extractor = extractors.FirstOrDefault(x => x.CanHandle(contentType, fileName));
        return extractor ?? throw new DomainLayer.Studio.StudioBusinessException("UNSUPPORTED_DOCUMENT_TYPE", 400, "Chỉ hỗ trợ PDF, DOCX, TXT.");
    }
}
