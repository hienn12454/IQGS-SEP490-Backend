using ApplicationLayer.DTOs.Profile;
using ApplicationLayer.Interfaces;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers;

/// <summary>SCRUM-161: Xem và cập nhật hồ sơ cá nhân.</summary>
[ApiController]
[Route("api/users/me")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    /// <summary>Xem hồ sơ của chính mình (kèm thông tin role-specific).</summary>
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        var result = await _profileService.GetProfileAsync(userId);
        return SuccessResp.Ok(result);
    }

    /// <summary>Cập nhật hồ sơ HR Manager (AC-01 SCRUM-161).</summary>
    [HttpPut("hr-profile")]
    [Authorize(Roles = "HR")]
    public async Task<IActionResult> UpdateHRProfile([FromBody] UpdateHRProfileDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _profileService.UpdateHRProfileAsync(userId, dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Cập nhật hồ sơ Candidate (AC-02 SCRUM-161).</summary>
    [HttpPut("candidate-profile")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> UpdateCandidateProfile([FromBody] UpdateCandidateProfileDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _profileService.UpdateCandidateProfileAsync(userId, dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Đổi mật khẩu — yêu cầu xác nhận mật khẩu hiện tại (AC-05 SCRUM-161).</summary>
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = GetCurrentUserId();
        await _profileService.ChangePasswordAsync(userId, dto);
        return SuccessResp.Ok("Mật khẩu đã được thay đổi thành công. Vui lòng đăng nhập lại.");
    }

    // ────────────────────────────────────────────────────────────────
    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedException("Không thể xác định người dùng từ token.");
        return Guid.Parse(sub);
    }
}
