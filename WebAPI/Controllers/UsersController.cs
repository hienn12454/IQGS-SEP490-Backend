using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.Profile;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    // ── Admin endpoints ──────────────────────────────────────────────

    /// <summary>Danh sách user phân trang, tìm kiếm theo tên/email, lọc theo vai trò và trạng thái.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUsers([FromQuery] UserQueryDto query)
    {
        var result = await _userService.GetUsersAsync(query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Chi tiết đầy đủ của một user (chỉ đọc).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserDetail(Guid id)
    {
        var result = await _userService.GetUserDetailAsync(id);
        return SuccessResp.Ok(result);
    }

    /// <summary>Vô hiệu hóa hoặc kích hoạt lại tài khoản.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusDto dto)
    {
        await _userService.UpdateUserStatusAsync(id, dto.IsActive);
        var msg = dto.IsActive
            ? "Tài khoản đã được kích hoạt thành công."
            : "Tài khoản đã bị vô hiệu hóa thành công.";
        return SuccessResp.Ok(msg);
    }

    // ── Me endpoints ─────────────────────────────────────────────────

    /// <summary>Xem hồ sơ của chính mình (kèm thông tin role-specific).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = GetCurrentUserId();
        var result = await _userService.GetProfileAsync(userId);
        return SuccessResp.Ok(result);
    }

    /// <summary>Cập nhật hồ sơ HR Manager.</summary>
    [HttpPatch("me/hr-profile")]
    [Authorize(Roles = "HR")]
    public async Task<IActionResult> UpdateHRProfile([FromBody] UpdateHRProfileDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _userService.UpdateHRProfileAsync(userId, dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Cập nhật hồ sơ Candidate.</summary>
    [HttpPatch("me/candidate-profile")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> UpdateCandidateProfile([FromBody] UpdateCandidateProfileDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _userService.UpdateCandidateProfileAsync(userId, dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Đổi mật khẩu — yêu cầu xác nhận mật khẩu hiện tại.</summary>
    [HttpPatch("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = GetCurrentUserId();
        await _userService.ChangePasswordAsync(userId, dto);
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
