using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

/// <summary>SCRUM-398: HR xem overview ứng viên (profile + stats + practice trên bộ của mình).</summary>
[ApiController]
[Route("api/hr/candidates")]
[Authorize(Roles = "HR")]
public class HrCandidatesController : ControllerBase
{
    private readonly IHrCandidateOverviewService _overviewService;

    public HrCandidatesController(IHrCandidateOverviewService overviewService)
    {
        _overviewService = overviewService;
    }

    /// <summary>
    /// Overview ứng viên: profile, stats COMPLETED toàn hệ thống, achievements, danh sách session trên set của HR.
    /// Yêu cầu: candidate bật AllowRecruiterRecommendation và đã có ≥1 session trên set của HR này.
    /// </summary>
    [HttpGet("{candidateUserId:guid}/overview")]
    public async Task<IActionResult> GetOverview(Guid candidateUserId)
    {
        var result = await _overviewService.GetOverviewAsync(candidateUserId, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    // JWT MapInboundClaims=false → claim id nằm ở "sub", không phải NameIdentifier.
    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("Không xác định được người dùng từ token đăng nhập.");

        return userId;
    }
}
