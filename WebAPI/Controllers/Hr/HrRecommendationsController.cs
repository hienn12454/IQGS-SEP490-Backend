using ApplicationLayer.DTOs.Recommendation;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

/// <summary>Dashboard recommendation của HR (SCRUM-291): xem candidate được hệ thống đề xuất theo rule (điểm ≥ 70, có consent, bộ PUBLISHED), shortlist/dismiss/gửi lời mời.</summary>
[ApiController]
[Route("api/hr/recommendations")]
[Authorize(Roles = "HR")]
public class HrRecommendationsController : ControllerBase
{
    private readonly IRecommendationService _service;

    public HrRecommendationsController(IRecommendationService service)
    {
        _service = service;
    }

    /// <summary>Danh sách candidate được đề xuất cho HR hiện tại, sắp theo điểm giảm dần, phân trang. Kèm thông tin candidate (tên, email, targetRole, techStack) + bộ câu hỏi + trạng thái lời mời nếu đã mời. Nếu candidate đã ACCEPTED lời mời, còn có invitationResponseMessage/invitationSharedPhoneNumber — do chính candidate chủ động gửi kèm lúc accept, không phải SĐT trên profile. Candidate đang tắt "Cho phép đề xuất hồ sơ cho HR" (allowRecruiterRecommendation, xem /api/candidate/privacy-settings) sẽ KHÔNG xuất hiện trong danh sách này, kể cả recommendation đã tạo từ trước.</summary>
    /// <param name="query">page, pageSize, status (NEW/SHORTLISTED/DISMISSED/INVITED), questionSetId.</param>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] HrRecommendationListQueryDto query)
    {
        var result = await _service.ListForHrAsync(GetCurrentUserId(), query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Shortlist 1 recommendation (đánh dấu quan tâm). 409 nếu đã gửi lời mời.</summary>
    /// <param name="id">Id recommendation.</param>
    [HttpPost("{id:guid}/shortlist")]
    public async Task<IActionResult> Shortlist(Guid id)
    {
        var result = await _service.ShortlistAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Dismiss 1 recommendation (bỏ qua — không hiện trong tab NEW/SHORTLISTED nữa). 409 nếu đã gửi lời mời.</summary>
    /// <param name="id">Id recommendation.</param>
    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id)
    {
        var result = await _service.DismissAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Gửi lời mời phỏng vấn cho candidate từ recommendation (kèm lời nhắn tùy chọn) — candidate sẽ thấy trong GET /api/candidate/invitations. Mỗi recommendation chỉ mời được 1 lần: 409 nếu đã mời hoặc đang DISMISSED.</summary>
    /// <param name="id">Id recommendation.</param>
    /// <param name="dto">message: lời nhắn tùy chọn (tối đa 2000 ký tự).</param>
    [HttpPost("{id:guid}/invite")]
    public async Task<IActionResult> Invite(Guid id, [FromBody] InviteCandidateRequestDto dto)
    {
        var result = await _service.InviteAsync(id, GetCurrentUserId(), dto);
        return SuccessResp.Created(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedException("Không thể xác định người dùng từ token.");
        return Guid.Parse(userIdStr);
    }
}
