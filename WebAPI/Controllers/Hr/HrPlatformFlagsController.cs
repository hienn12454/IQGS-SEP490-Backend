using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Hr;

/// <summary>SCRUM-464: cờ nền tảng đọc-only cho HR (không dùng admin API).</summary>
[ApiController]
[Route("api/hr/platform-flags")]
[Authorize(Roles = "HR")]
public class HrPlatformFlagsController : ControllerBase
{
    private readonly IPlatformSettingsRepository _platformSettings;

    public HrPlatformFlagsController(IPlatformSettingsRepository platformSettings)
    {
        _platformSettings = platformSettings;
    }

    /// <summary>Công tắc anti-cheat toàn hệ thống (Admin) — HR dùng để enable/disable toggle trên bộ Tuyển.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var settings = await _platformSettings.GetAsync();
        return SuccessResp.Ok(new
        {
            antiCheatEnabled = settings.AntiCheatEnabled,
            antiCheatMaxTabLeaves = settings.AntiCheatMaxTabLeaves
        });
    }
}
