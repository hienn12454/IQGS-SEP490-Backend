using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Interfaces;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using InfrastructureLayer.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Services.Studio;

/// <summary>
/// Upload JD file cho Studio: PDF/DOCX/TXT qua RAG ParseJd (fallback extractor local),
/// ảnh JPG/PNG qua OCR pattern ParseCv (Summary text). Validate IT trước khi lưu (SCRUM-416).
/// </summary>
public sealed class StudioJobDescriptionUploadService(
    AppDbContext dbContext,
    IInterviewProjectService projectService,
    IJobDescriptionAnalyzer analyzer,
    IDocumentTextExtractorFactory extractorFactory,
    IRagService ragService,
    IBlobStorageService blobStorage) : IStudioJobDescriptionUploadService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt", ".jpg", ".jpeg", ".png"
    };

    private const long MaxFileBytes = 20 * 1024 * 1024;

    public async Task<UploadJobDescriptionResponse> UploadAsync(
        Guid projectId,
        Guid userId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, requireEdit: true, ct);

        if (content.Length == 0)
            throw new StudioBusinessException("DOCUMENT_PAYLOAD_REQUIRED", StatusCodes.Status400BadRequest, "File rỗng.");

        if (content.Length > MaxFileBytes)
            throw new StudioBusinessException("DOCUMENT_TOO_LARGE", StatusCodes.Status400BadRequest, "File JD tối đa 20 MB.");

        var safeName = SanitizeFileName(fileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            throw new StudioBusinessException("INVALID_DOCUMENT_TYPE", StatusCodes.Status400BadRequest, "Chỉ hỗ trợ PDF, DOCX, TXT, JPG, JPEG, PNG.");

        var extracted = await ExtractTextAsync(safeName, contentType, content, ct);
        if (string.IsNullOrWhiteSpace(extracted))
            throw new StudioBusinessException("JD_EMPTY_AFTER_EXTRACT", StatusCodes.Status422UnprocessableEntity, "Không trích xuất được nội dung từ file JD.");

        // SCRUM-416: validate cấu trúc + domain IT (keyword) trước LLM.
        var text = JobDescriptionValidator.Validate(extracted, safeName);

        // SCRUM-432: classify IT job posting TRƯỚC SaveChanges — fail → không ghi DB.
        var summary = await analyzer.AnalyzeAsync(text, ct);

        // SCRUM-465: lưu file gốc lên blob (preview candidate)
        var contentTypeNorm = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        var blobPath = BlobPathHelper.BuildStudioJobDescriptionPath(projectId, safeName);
        await using (var ms = new MemoryStream(content, writable: false))
            await blobStorage.UploadAsync(ms, contentTypeNorm, blobPath, ct);

        var row = await dbContext.StudioJobDescriptions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (row is null)
        {
            row = new JobDescription
            {
                ProjectId = projectId,
                Content = text,
                SourceType = JobDescriptionSourceType.UploadedFile,
                OriginalFileName = safeName,
                BlobPath = blobPath
            };
            dbContext.StudioJobDescriptions.Add(row);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(row.BlobPath) && !string.Equals(row.BlobPath, blobPath, StringComparison.Ordinal))
            {
                try { await blobStorage.DeleteAsync(row.BlobPath); }
                catch { /* ignore */ }
            }
            row.Content = text;
            row.SourceType = JobDescriptionSourceType.UploadedFile;
            row.OriginalFileName = safeName;
            row.BlobPath = blobPath;
            row.UpdatedAt = DateTime.UtcNow;
        }

        row.WordCount = text.Split(new char[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        row.CharacterCount = text.Length;
        row.DetectedRole = summary.DetectedRole;
        row.DetectedSeniority = summary.DetectedSeniority;
        row.DetectedLanguage = summary.DetectedLanguage;
        row.DetectedSkillsJson = System.Text.Json.JsonSerializer.Serialize(summary.Skills);
        row.Title = FirstNonEmpty(summary.Position, summary.DetectedRole);
        row.ExtractedInformationJson = ApplicationLayer.Studio.Helpers.StudioAiConfigurationHelper.SerializeExtractedInformation(
            summary.Responsibilities ?? [],
            summary.Summary);

        await dbContext.SaveChangesAsync(ct);

        summary = summary with { Position = row.Title };

        return new UploadJobDescriptionResponse(
            text,
            safeName,
            JobDescriptionSourceType.UploadedFile,
            row.WordCount,
            row.CharacterCount,
            summary);
    }

    private async Task<string> ExtractTextAsync(string fileName, string contentType, byte[] content, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var isImage = ext is ".jpg" or ".jpeg" or ".png";

        if (isImage)
            return await ExtractFromImageAsync(fileName, content, ct);

        try
        {
            await using var stream = new MemoryStream(content);
            var parse = await ragService.ParseJdAsync(stream, fileName, ct);
            if (parse.Success && !string.IsNullOrWhiteSpace(parse.JobDescription))
                return parse.JobDescription!;
        }
        catch
        {
            // Fallback local extractors bên dưới
        }

        try
        {
            var extractor = extractorFactory.Resolve(contentType, fileName);
            return await extractor.ExtractTextAsync(content, ct);
        }
        catch (StudioBusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("DOCUMENT_PROCESSING_FAILED", StatusCodes.Status422UnprocessableEntity, $"Không đọc được file JD: {ex.Message}");
        }
    }

    private async Task<string> ExtractFromImageAsync(string fileName, byte[] content, CancellationToken ct)
    {
        try
        {
            await using var stream = new MemoryStream(content);
            var parse = await ragService.ParseCvAsync(stream, fileName, ct);
            if (!parse.Success)
                throw new StudioBusinessException("DOCUMENT_PROCESSING_FAILED", StatusCodes.Status422UnprocessableEntity,
                    parse.Error ?? parse.Detail ?? "OCR ảnh JD thất bại.");

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(parse.Summary))
                parts.Add(parse.Summary!);
            if (parse.Skills is { Count: > 0 })
                parts.Add("Skills: " + string.Join(", ", parse.Skills));
            if (!string.IsNullOrWhiteSpace(parse.FullName))
                parts.Add("Name: " + parse.FullName);

            var text = string.Join("\n", parts);
            if (string.IsNullOrWhiteSpace(text))
                throw new StudioBusinessException("JD_EMPTY_AFTER_EXTRACT", StatusCodes.Status422UnprocessableEntity,
                    "OCR không trả về nội dung text từ ảnh JD.");

            return text;
        }
        catch (StudioBusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("DOCUMENT_PROCESSING_FAILED", StatusCodes.Status422UnprocessableEntity,
                $"OCR ảnh JD thất bại: {ex.Message}");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? "jd.bin");
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "jd.bin" : name;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }
}
