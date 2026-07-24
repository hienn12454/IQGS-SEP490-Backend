using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Studio;

/// <summary>
/// Controller MỚI: Studio Sources → RAG (y hệt Knowledge Base HR).
/// Upload blob + knowledge_documents + Hangfire ingest.
/// SCRUM-373: list/attach từ Knowledge Documents đã có.
/// </summary>
[ApiController]
[Authorize(Roles = "HR")]
[Route("api/studio/projects/{projectId:guid}/knowledge-documents")]
public sealed class StudioKnowledgeRagController(IStudioKnowledgeDocumentService documentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, CancellationToken ct)
        => Ok(await documentService.ListAsync(projectId, GetUserId(), ct));

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid projectId, IFormFile file, [FromForm] bool isSelected = true, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { errorCode = "DOCUMENT_FILE_REQUIRED", detail = "Thiếu file upload." });

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var result = await documentService.UploadAsync(
            projectId,
            GetUserId(),
            new UploadStudioDocumentRequest(file.FileName, file.ContentType ?? "application/octet-stream", ms.ToArray(), isSelected),
            ct);
        return Ok(result);
    }

    /// <summary>SCRUM-373: list Knowledge Documents HR để chọn gắn vào project.</summary>
    [HttpGet("library")]
    public async Task<IActionResult> ListLibrary(Guid projectId, CancellationToken ct)
        => Ok(await documentService.ListLibraryAsync(projectId, GetUserId(), ct));

    /// <summary>SCRUM-373: gắn doc đã upload ở /hr/knowledge vào Studio (không upload lại).</summary>
    [HttpPost("attach")]
    public async Task<IActionResult> AttachFromLibrary(
        Guid projectId,
        [FromBody] AttachStudioDocumentsRequest request,
        CancellationToken ct)
        => Ok(await documentService.AttachFromLibraryAsync(projectId, GetUserId(), request, ct));

    [HttpPatch("{documentId:guid}/selection")]
    public async Task<IActionResult> SetSelection(Guid projectId, Guid documentId, [FromBody] SetSelectionBody body, CancellationToken ct)
        => Ok(await documentService.SetSelectionAsync(projectId, documentId, GetUserId(), body.IsSelected, ct));

    [HttpPost("{documentId:guid}/reingest")]
    public async Task<IActionResult> Reingest(Guid projectId, Guid documentId, CancellationToken ct)
        => Ok(await documentService.ReingestAsync(projectId, documentId, GetUserId(), ct));

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid documentId, CancellationToken ct)
    {
        await documentService.DeleteAsync(projectId, documentId, GetUserId(), ct);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(sub!);
    }

    public sealed record SetSelectionBody(bool IsSelected);
}
