using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/knowledge-documents")]
[Authorize(Roles = "Admin")]
public class AdminKnowledgeDocumentsController : ControllerBase
{
    private readonly IKnowledgeDocumentService _service;

    public AdminKnowledgeDocumentsController(IKnowledgeDocumentService service)
    {
        _service = service;
    }

    /// <summary>Upload tài liệu SYSTEM — body chỉ cần file.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] AdminKnowledgeDocumentUploadForm form)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest(new { Code = 400, Error = "File là bắt buộc." });

        var dto = new KnowledgeDocumentUploadDto
        {
            Scope = KnowledgeDocumentScope.System,
            OwnerId = null
        };

        await using var stream = form.File.OpenReadStream();
        var result = await _service.UploadAsync(
            stream, form.File.FileName, form.File.ContentType, form.File.Length,
            dto, GetCurrentUserId());

        return SuccessResp.Accepted(new { documentId = result.DocumentId, status = result.Status });
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] KnowledgeDocumentListQueryDto query)
    {
        var result = await _service.GetPagedAsync(query, KnowledgeDocumentScope.System);
        return SuccessResp.Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return SuccessResp.Ok(result);
    }

    [HttpPost("{id:guid}/reingest")]
    public async Task<IActionResult> Reingest(Guid id)
    {
        var result = await _service.ReingestAsync(id);
        return SuccessResp.Accepted(new { documentId = result.DocumentId, status = result.Status });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return SuccessResp.NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdStr!);
    }
}

/// <summary>Form upload admin — chỉ cần file.</summary>
public class AdminKnowledgeDocumentUploadForm
{
    public IFormFile File { get; set; } = null!;
}
