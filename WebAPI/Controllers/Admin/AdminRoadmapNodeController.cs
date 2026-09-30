using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.ResponseCode;
using ApplicationLayer.Services.Coach;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

/// <summary>SCRUM-455/486: import curated roadmap nodes + gắn KnowledgeDocument.</summary>
[ApiController]
[Route("api/admin/roadmap-nodes")]
[Authorize(Roles = "Admin")]
public class AdminRoadmapNodeController : ControllerBase
{
    private readonly IRoadmapNodeImportService _import;

    public AdminRoadmapNodeController(IRoadmapNodeImportService import) => _import = import;

    [HttpGet]
    public async Task<IActionResult> List()
        => SuccessResp.Ok(await _import.ListAsync());

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] RoadmapNodeImportRequestDto payload)
    {
        var result = await _import.ImportAsync(payload);
        if (!result.Success)
            return BadRequest(new { Code = 400, Error = "Roadmap node không hợp lệ.", Data = result });
        return SuccessResp.Ok(result);
    }

    /// <summary>Import JSONL (mỗi dòng 1 node) — cùng schema với docs/kb-seed/roadmap/*.jsonl.</summary>
    [HttpPost("import-jsonl")]
    [Consumes("text/plain", "application/x-ndjson", "application/json")]
    public async Task<IActionResult> ImportJsonl()
    {
        using var reader = new StreamReader(Request.Body);
        var text = await reader.ReadToEndAsync();
        var result = await _import.ImportJsonlAsync(text);
        if (!result.Success)
            return BadRequest(new { Code = 400, Error = "Roadmap JSONL không hợp lệ.", Data = result });
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-486: gắn/gỡ KnowledgeDocumentId.</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateLink(Guid id, [FromBody] UpdateRoadmapNodeLinkDto body)
    {
        var result = await _import.UpdateLinkAsync(id, body);
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-486: gắn doc vào nodes có SourceUrl khớp FileName.</summary>
    [HttpPost("link-by-filename")]
    public async Task<IActionResult> LinkByFilename([FromBody] LinkRoadmapNodesByFilenameDto body)
    {
        if (body.KnowledgeDocumentId == Guid.Empty)
            return BadRequest(new { Code = 400, Error = "KnowledgeDocumentId là bắt buộc." });
        var result = await _import.LinkByFilenameAsync(body);
        return SuccessResp.Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _import.DeleteAsync(id);
        return SuccessResp.Ok(new { id });
    }
}
