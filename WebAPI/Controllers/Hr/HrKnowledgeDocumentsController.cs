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
            OwnerId = ownerId
        };

        await using var stream = form.File.OpenReadStream();
        var result = await _service.UploadAsync(
            stream, form.File.FileName, form.File.ContentType, form.File.Length,
            dto, ownerId);

        return SuccessResp.Accepted(new { documentId = result.DocumentId, status = result.Status });
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
}
