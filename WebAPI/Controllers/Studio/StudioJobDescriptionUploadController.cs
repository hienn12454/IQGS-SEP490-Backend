using ApplicationLayer.Studio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Studio;

/// <summary>
/// Controller MỚI cho upload JD file (PDF/DOCX/TXT/ảnh) trong luồng Studio.
/// Không gộp vào StudioJobDescriptionsController — mọi API mới của Studio vào controller mới.
/// </summary>
[ApiController]
[Authorize(Roles = "HR")]
[Route("api/studio/projects/{projectId:guid}/job-description")]
public sealed class StudioJobDescriptionUploadController(IStudioJobDescriptionUploadService uploadService) : ControllerBase
{
    /// <summary>
    /// Upload file JD → extract/OCR → lưu JD → trả content + Auto-detected summary.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid projectId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { errorCode = "DOCUMENT_FILE_REQUIRED", detail = "Thiếu file upload." });

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var result = await uploadService.UploadAsync(
            projectId,
            GetUserId(),
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            ms.ToArray(),
            ct);

        return Ok(result);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(sub!);
    }
}
