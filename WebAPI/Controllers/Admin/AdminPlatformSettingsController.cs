using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

/// <summary>Cấu hình toàn hệ thống do Admin chỉnh runtime — không cần sửa appsettings.json hay redeploy.</summary>
[ApiController]
[Route("api/admin/platform-settings")]
[Authorize(Roles = "Admin")]
public class AdminPlatformSettingsController : ControllerBase
{
    private readonly IPlatformSettingsService _service;

    public AdminPlatformSettingsController(IPlatformSettingsService service)
    {
        _service = service;
    }

    /// <summary>Xem cấu hình hệ thống hiện tại (vd số câu hỏi tối thiểu để HR publish bộ câu hỏi lên marketplace).</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _service.GetAsync();
        return SuccessResp.Ok(result);
    }

    /// <summary>Chỉnh cấu hình hệ thống — vd hạ số câu hỏi tối thiểu để publish xuống 1 để tiện test trên web, không cần sửa code/redeploy.</summary>
    /// <param name="dto">minQuestionsToPublish: 1-100.</param>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdatePlatformSettingsDto dto)
    {
        var result = await _service.UpdateAsync(dto);
        return SuccessResp.Ok(result);
    }
}
