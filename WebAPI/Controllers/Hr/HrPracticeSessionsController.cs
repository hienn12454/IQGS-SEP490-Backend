using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

[ApiController]
[Route("api/hr/practice-sessions")]
[Authorize(Roles = "HR")]
public class HrPracticeSessionsController : ControllerBase
{
    private readonly IHrPracticeSessionFeedbackService _feedbackService;

    public HrPracticeSessionsController(IHrPracticeSessionFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    /// <summary>
    /// AI feedback phiên luyện tập trên bộ của HR — không áp dụng teaser Free của candidate.
    /// 403 nếu không phải chủ bộ / candidate tắt consent. 400 nếu phiên chưa COMPLETED.
    /// </summary>
    [HttpGet("{sessionId:guid}/feedback")]
    public async Task<IActionResult> GetFeedback(Guid sessionId)
    {
        var result = await _feedbackService.GetFeedbackForHrAsync(sessionId, GetCurrentUserId());
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
