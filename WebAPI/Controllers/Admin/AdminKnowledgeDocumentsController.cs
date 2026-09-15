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

    /// <summary>Upload tài liệu SYSTEM — chọn type (InternalStack/Roadmap/...).</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] AdminKnowledgeDocumentUploadForm form)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest(new { Code = 400, Error = "File là bắt buộc." });

        var dto = new KnowledgeDocumentUploadDto
        {
            Scope = KnowledgeDocumentScope.System,
            OwnerId = null,
            DocumentType = form.DocumentType,
            AdminNote = form.AdminNote,
            Folder = form.Folder
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

    /// <summary>SCRUM-450: danh sách folder UI + số lượng file.</summary>
    [HttpGet("folders")]
    public async Task<IActionResult> ListFolders()
    {
        var result = await _service.ListFoldersAsync(KnowledgeDocumentScope.System);
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-451: đổi tên folder (metadata bulk, không move Blob).</summary>
    [HttpPost("folders/rename")]
    public async Task<IActionResult> RenameFolder([FromBody] RenameKnowledgeFolderDto body)
    {
        if (string.IsNullOrWhiteSpace(body.From))
            return BadRequest(new { Code = 400, Error = "From là bắt buộc." });

        var result = await _service.RenameFolderAsync(
            KnowledgeDocumentScope.System, body.From, body.To);
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-451: chuyển nhiều document sang folder.</summary>
    [HttpPost("move")]
    public async Task<IActionResult> MoveDocuments([FromBody] MoveKnowledgeDocumentsDto body)
    {
        if (body.DocumentIds is null || body.DocumentIds.Count == 0)
            return BadRequest(new { Code = 400, Error = "DocumentIds là bắt buộc." });

        var result = await _service.MoveDocumentsAsync(
            KnowledgeDocumentScope.System, body.DocumentIds, body.Folder);
        return SuccessResp.Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-447/450: PATCH type + AdminNote + Folder.</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateMeta(Guid id, [FromBody] UpdateKnowledgeDocumentMetaDto body)
    {
        var result = await _service.UpdateDocumentMetaAsync(
            id,
            body.DocumentType,
            body.AdminNote,
            body.Folder,
            updateFolder: body.Folder is not null || body.ClearFolder,
            ownerIdFilter: null);
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-444/447: preview vài chunk đầu.</summary>
    [HttpGet("{id:guid}/chunks")]
    public async Task<IActionResult> GetChunks(Guid id, [FromQuery] int take = 20)
    {
        var result = await _service.GetChunksAsync(id, ownerIdFilter: null, take);
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

/// <summary>Form upload admin — file + type Coach KB.</summary>
public class AdminKnowledgeDocumentUploadForm
{
    public IFormFile File { get; set; } = null!;

    /// <summary>SCRUM-447: InternalStack (Tech) | Roadmap | …</summary>
    public string? DocumentType { get; set; }

    public string? AdminNote { get; set; }

    /// <summary>SCRUM-450: nhóm folder UI (vd. swe).</summary>
    public string? Folder { get; set; }
}
