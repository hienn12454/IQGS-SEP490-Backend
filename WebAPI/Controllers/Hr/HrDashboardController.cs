using ApplicationLayer.DTOs.Hr;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

/// <summary>SCRUM-339: dashboard tổng hợp cho HR — gộp KPI, hoạt động theo ngày, phân bố loại câu hỏi, phiên gần đây, phân tích AI và candidate nổi bật vào 1 API duy nhất.</summary>
[ApiController]
[Route("api/hr/dashboard")]
[Authorize(Roles = "HR")]
public class HrDashboardController : ControllerBase
{
    private readonly IHrDashboardService _service;

    public HrDashboardController(IHrDashboardService service)
    {
        _service = service;
    }

    /// <summary>Toàn bộ dữ liệu dashboard của HR hiện tại trong 1 request duy nhất.</summary>
    /// <param name="query">activityDays (mặc định 30), recentLimit (mặc định 7), recommendationsLimit (mặc định 8).</param>
    [HttpGet]
    public async Task<IActionResult> GetDashboard([FromQuery] HrDashboardQueryDto query)
    {
        var result = await _service.GetDashboardAsync(GetCurrentUserId(), query);
        return SuccessResp.Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedException("Không thể xác định người dùng từ token.");
        return Guid.Parse(userIdStr);
    }
}
