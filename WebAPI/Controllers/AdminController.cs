using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.Interfaces;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

/// <summary>
/// SCRUM-163: Quản lý người dùng cho Admin.
/// AC-06: Chỉ Admin mới truy cập được.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    // ────────────────────────────────────────────────────────────────
    // GET api/admin/users?page=1&pageSize=10&search=&role=&isActive=
    // AC-01/02: Bảng phân trang + tìm kiếm + lọc
    // ────────────────────────────────────────────────────────────────
    /// <summary>Danh sách user phân trang, tìm kiếm theo tên/email, lọc theo vai trò và trạng thái.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] UserQueryDto query)
    {
        var result = await _adminService.GetUsersAsync(query);
        // AC-05: frontend check result.TotalCount == 0 để hiện "Không tìm thấy người dùng."
        return SuccessResp.Ok(result);
    }

    // ────────────────────────────────────────────────────────────────
    // GET api/admin/users/{id}   AC-03
    // ────────────────────────────────────────────────────────────────
    /// <summary>Chi tiết đầy đủ của một user (chỉ đọc).</summary>
    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUserDetail(Guid id)
    {
        var result = await _adminService.GetUserDetailAsync(id);
        return SuccessResp.Ok(result);
    }

    // ────────────────────────────────────────────────────────────────
    // PATCH api/admin/users/{id}/status   AC-04
    // ────────────────────────────────────────────────────────────────
    /// <summary>Vô hiệu hóa hoặc kích hoạt lại tài khoản. Thu hồi session ngay khi disable.</summary>
    [HttpPatch("users/{id:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusDto dto)
    {
        await _adminService.UpdateUserStatusAsync(id, dto.IsActive);
        var msg = dto.IsActive
            ? "Tài khoản đã được kích hoạt thành công."
            : "Tài khoản đã bị vô hiệu hóa thành công.";
        return SuccessResp.Ok(msg);
    }
}
