using ApplicationLayer.DTOs.Hr;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

[ApiController]
[Route("api/hr/talent")]
[Authorize(Roles = "HR")]
public class HrTalentController : ControllerBase
{
    private readonly IHrTalentService _service;

    public HrTalentController(IHrTalentService service)
    {
        _service = service;
    }

    /// <summary>Talent pool: mọi phiên luyện trên bộ của HR (consent bật), paging server-side.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] HrTalentListQueryDto query)
    {
        var result = await _service.ListAsync(GetCurrentUserId(), query);
        return SuccessResp.Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("Không xác định được người dùng từ token đăng nhập.");

        return userId;
    }
}
