using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

[ApiController]
[Route("api/hr/knowledge-documents")]
[Authorize(Roles = "HR")]
public class HrKnowledgeDocumentsController : ControllerBase
{
    private readonly IKnowledgeDocumentService _service;

    public HrKnowledgeDocumentsController(IKnowledgeDocumentService service)
    {
        _service = service;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] HrKnowledgeDocumentUploadForm form)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest(new { Code = 400, Error = "File là bắt buộc." });

        var ownerId = GetCurrentUserId();
        var dto = new KnowledgeDocumentUploadDto
        {
            Scope = KnowledgeDocumentScope.Hr,
            OwnerId = ownerId,
            // SCRUM-442: HR bắt buộc DocumentType
            DocumentType = form.DocumentType
        };

        await using var stream = form.File.OpenReadStream();
        var result = await _service.UploadAsync(
            stream, form.File.FileName, form.File.ContentType, form.File.Length,
            dto, ownerId);

        // Trả đủ field để FE map ngay (không hiện "Unknown file" rồi mất sau reload).
        return SuccessResp.Accepted(new
        {
            documentId = result.DocumentId,
            fileName = result.FileName,
            status = result.Status,
            documentType = result.DocumentType
        });
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] KnowledgeDocumentListQueryDto query)
    {
        var result = await _service.GetPagedAsync(
            query, KnowledgeDocumentScope.Hr, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-442: đổi loại tài liệu (Policy / InternalStack / Rubric / RolePack).</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateType(Guid id, [FromBody] UpdateKnowledgeDocumentTypeDto body)
    {
        var result = await _service.UpdateDocumentTypeAsync(
            id, body.DocumentType, GetCurrentUserId(), requireHrType: true);
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-444: preview vài chunk đầu.</summary>
    [HttpGet("{id:guid}/chunks")]
    public async Task<IActionResult> GetChunks(Guid id, [FromQuery] int take = 20)
    {
        var result = await _service.GetChunksAsync(id, GetCurrentUserId(), take);
        return SuccessResp.Ok(result);
    }

    [HttpPost("{id:guid}/reingest")]
    public async Task<IActionResult> Reingest(Guid id)
    {
        var result = await _service.ReingestAsync(id, GetCurrentUserId());
        return SuccessResp.Accepted(new { documentId = result.DocumentId, status = result.Status });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id, GetCurrentUserId());
        return SuccessResp.NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdStr!);
    }
}

public class HrKnowledgeDocumentUploadForm
{
    public IFormFile File { get; set; } = null!;

    /// <summary>SCRUM-442: Policy | InternalStack | Rubric | RolePack</summary>
    public string DocumentType { get; set; } = string.Empty;
}
