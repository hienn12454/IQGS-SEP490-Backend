using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.ResponseCode;
using ApplicationLayer.Services.Coach;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

/// <summary>
/// SCRUM-453: quản trị competency framework bằng DATA.
/// Thêm role/technology mới (Java, Python, React...) chỉ cần import JSON curated qua đây.
/// </summary>
[ApiController]
[Route("api/admin/competency-frameworks")]
[Authorize(Roles = "Admin")]
public class AdminCompetencyFrameworkController : ControllerBase
{
    private readonly ICompetencyFrameworkImportService _import;

    public AdminCompetencyFrameworkController(ICompetencyFrameworkImportService import)
        => _import = import;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var result = await _import.ListAsync();
        return SuccessResp.Ok(result);
    }

    /// <summary>Dry-run: báo lỗi/cảnh báo trước khi ghi DB.</summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] CompetencyFrameworkImportDto payload)
    {
        var result = await _import.ValidateAsync(payload);
        return SuccessResp.Ok(result);
    }

    /// <summary>Upsert idempotent theo (roleKey, level) — chạy lại cùng file không tạo bản trùng.</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] CompetencyFrameworkImportDto payload)
    {
        var result = await _import.ImportAsync(payload);
        if (!result.Success)
            return BadRequest(new { Code = 400, Error = "Framework không hợp lệ.", Data = result });
        return SuccessResp.Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateCompetencyFrameworkStatusDto dto)
    {
        await _import.UpdateStatusAsync(id, dto.Status);
        return SuccessResp.Ok(new { id, status = dto.Status });
    }
}
