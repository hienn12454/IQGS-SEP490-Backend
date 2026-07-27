using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

/// <summary>Cài đặt quyền riêng tư của Candidate — consent cho phép hệ thống recommend hồ sơ cho HR (SCRUM-293).</summary>
[ApiController]
[Route("api/candidate/privacy-settings")]
[Authorize(Roles = "Candidate")]
public class CandidatePrivacySettingsController : ControllerBase
{
    private readonly ICandidatePrivacySettingsService _service;

    public CandidatePrivacySettingsController(ICandidatePrivacySettingsService service)
    {
        _service = service;
    }

    /// <summary>Xem cài đặt quyền riêng tư hiện tại — allowRecruiterRecommendation mặc định true nếu chưa từng đổi.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _service.GetAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Bật/tắt consent allowRecruiterRecommendation. Bật = phiên luyện tập đạt điều kiện sẽ tự tạo recommendation cho HR, và toàn bộ recommendation hiện có (kể cả tạo từ trước) hiển thị lại trên list HR. Tắt = không tạo recommendation mới, đồng thời ẩn NGAY mọi recommendation đã tạo trước đó khỏi list HR xem được (không xóa dữ liệu, chỉ ẩn — bật lại là thấy lại).</summary>
    /// <param name="dto">allowRecruiterRecommendation: true/false.</param>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] CandidatePrivacySettingsDto dto)
    {
        var result = await _service.UpdateAsync(User.GetUserId(), dto);
        return SuccessResp.Ok(result);
    }
}
