using ApplicationLayer.DTOs.Feedback;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers;

/// <summary>Feedback / đánh giá khách hàng — HR &amp; Candidate gửi, hiển thị công khai trên landing page, Admin duyệt.</summary>
[ApiController]
[Route("api/feedbacks")]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;

    public FeedbackController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    /// <summary>Danh sách feedback đã duyệt — public, dùng cho landing page.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetPublic([FromQuery] int limit = 20)
    {
        var result = await _feedbackService.GetPublicApprovedAsync(limit);
        return SuccessResp.Ok(result);
    }

    /// <summary>Gửi feedback mới (HR/Candidate). Mặc định ở trạng thái chờ duyệt (Pending).</summary>
    [Authorize(Roles = "HR,Candidate")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFeedbackDto dto)
    {
        var result = await _feedbackService.CreateAsync(GetCurrentUserId(), dto);
        return SuccessResp.Created(result);
    }

    /// <summary>Danh sách feedback phân trang cho Admin quản lý (tất cả trạng thái).</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    public async Task<IActionResult> GetForAdmin([FromQuery] FeedbackQueryDto query)
    {
        var result = await _feedbackService.GetPagedForAdminAsync(query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Admin sửa nội dung/rating của một feedback.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFeedbackDto dto)
    {
        var result = await _feedbackService.UpdateAsync(id, dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Admin duyệt/từ chối feedback (Pending → Approved/Rejected).</summary>
    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateFeedbackStatusDto dto)
    {
        var result = await _feedbackService.UpdateStatusAsync(id, dto.Status);
        return SuccessResp.Ok(result);
    }

    /// <summary>Admin xoá (mềm) feedback.</summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _feedbackService.DeleteAsync(id);
        return SuccessResp.NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedException("Không thể xác định người dùng từ token.");
        return Guid.Parse(sub);
    }
}
