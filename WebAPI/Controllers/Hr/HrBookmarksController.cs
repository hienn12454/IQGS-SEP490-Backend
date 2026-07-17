using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

/// <summary>SCRUM-324: bộ câu hỏi HR đã bookmark (của chính mình) — để mở lại nhanh các set quan trọng.</summary>
[ApiController]
[Route("api/hr/bookmarks")]
[Authorize(Roles = "HR")]
public class HrBookmarksController : ControllerBase
{
    private readonly IHrBookmarkService _service;

    public HrBookmarksController(IHrBookmarkService service)
    {
        _service = service;
    }

    /// <summary>Danh sách bộ câu hỏi HR hiện tại đã bookmark, mới nhất trước.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var result = await _service.ListBookmarkedAsync(GetCurrentUserId());
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
