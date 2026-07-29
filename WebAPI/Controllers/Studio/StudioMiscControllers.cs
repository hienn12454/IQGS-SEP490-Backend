using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Studio;

[ApiController]
[Authorize(Roles = "HR")]
[Route("api/studio/projects/{projectId:guid}/documents")]
public sealed class StudioKnowledgeDocumentsController(IStudioKnowledgeDocumentService documentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, CancellationToken ct) => Ok(await documentService.ListAsync(projectId, GetUserId(), ct));

    [HttpPost]
    [Consumes("multipart/form-data", "application/json")]
    public async Task<IActionResult> Upload(Guid projectId, CancellationToken ct)
    {
        UploadStudioDocumentRequest request;
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault() ?? throw new DomainLayer.Studio.StudioBusinessException("DOCUMENT_FILE_REQUIRED", 400, "Thiếu file upload.");
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var isSelected = bool.TryParse(form["isSelected"], out var selected) && selected;
            request = new UploadStudioDocumentRequest(file.FileName, file.ContentType ?? "application/octet-stream", ms.ToArray(), isSelected);
        }
        else
        {
            var body = await HttpContext.Request.ReadFromJsonAsync<UploadStudioDocumentApiRequest>(cancellationToken: ct)
                ?? throw new DomainLayer.Studio.StudioBusinessException("DOCUMENT_PAYLOAD_REQUIRED", 400, "Thiếu payload.");
            var bytes = Convert.FromBase64String(body.Base64Content);
            request = new UploadStudioDocumentRequest(body.FileName, body.ContentType, bytes, body.IsSelected);
        }
        var result = await documentService.UploadAsync(projectId, GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpPatch("{documentId:guid}/selection")]
    public async Task<IActionResult> SetSelection(Guid projectId, Guid documentId, [FromBody] SetDocumentSelectionRequest request, CancellationToken ct)
        => Ok(await documentService.SetSelectionAsync(projectId, documentId, GetUserId(), request.IsSelected, ct));

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid documentId, CancellationToken ct)
    {
        await documentService.DeleteAsync(projectId, documentId, GetUserId(), ct);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(sub!);
    }

    public sealed record UploadStudioDocumentApiRequest(string FileName, string ContentType, string Base64Content, bool IsSelected);
    public sealed record SetDocumentSelectionRequest(bool IsSelected);
}

[ApiController]
[Authorize(Roles = "HR")]
[Route("api/studio/projects/{projectId:guid}/settings")]
public sealed class StudioSettingsController(IStudioSettingsService settingsService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct) => Ok(await settingsService.GetAsync(projectId, GetUserId(), ct));

    [HttpPut]
    public async Task<IActionResult> Update(Guid projectId, [FromBody] UpdateStudioSettingsRequest request, CancellationToken ct)
        => Ok(await settingsService.UpdateAsync(projectId, GetUserId(), request, ct));

    private Guid GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(sub!);
    }
}

[ApiController]
[Authorize(Roles = "HR")]
[Route("api/studio/projects/{projectId:guid}/share-links")]
public sealed class StudioSharingController(IStudioShareService shareService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateShareLinkRequest request, CancellationToken ct)
        => Ok(await shareService.CreateAsync(projectId, GetUserId(), request, ct));

    [HttpDelete("{shareId:guid}")]
    public async Task<IActionResult> Revoke(Guid projectId, Guid shareId, CancellationToken ct)
    {
        await shareService.RevokeAsync(projectId, shareId, GetUserId(), ct);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("/api/studio/shared/{token}")]
    public async Task<IActionResult> Resolve(string token, CancellationToken ct)
        => Ok(await shareService.ResolveAsync(token, ct));

    private Guid GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(sub!);
    }
}
