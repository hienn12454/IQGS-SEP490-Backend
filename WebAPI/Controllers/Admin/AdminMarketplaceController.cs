using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

/// <summary>SCRUM-404: Admin quản lý Marketplace — list/detail/pin + KPI.</summary>
[ApiController]
[Route("api/admin/marketplace")]
[Authorize(Roles = "Admin")]
public class AdminMarketplaceController : ControllerBase
{
    private readonly IAdminMarketplaceService _service;

    public AdminMarketplaceController(IAdminMarketplaceService service)
    {
        _service = service;
    }

    /// <summary>KPI Marketplace: tổng published, practice 7 ngày, số pin, top HR/skill.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var result = await _service.GetStatsAsync();
        return SuccessResp.Ok(result);
    }

    /// <summary>Danh sách bộ PUBLISHED trên Marketplace kèm HR owner + practice stats.</summary>
    [HttpGet("question-sets")]
    public async Task<IActionResult> List([FromQuery] AdminMarketplaceListQueryDto query)
    {
        var result = await _service.ListAsync(query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Chi tiết bộ + practitioners (Admin không lọc consent).</summary>
    [HttpGet("question-sets/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return SuccessResp.Ok(result);
    }

    /// <summary>Ghim bộ lên đầu Marketplace (giới hạn MaxPinnedSets).</summary>
    [HttpPost("question-sets/{id:guid}/pin")]
    public async Task<IActionResult> Pin(Guid id)
    {
        var result = await _service.PinAsync(id);
        return SuccessResp.Ok(result);
    }

    /// <summary>Bỏ ghim bộ khỏi đầu Marketplace.</summary>
    [HttpDelete("question-sets/{id:guid}/pin")]
    public async Task<IActionResult> Unpin(Guid id)
    {
        var result = await _service.UnpinAsync(id);
        return SuccessResp.Ok(result);
    }
}
