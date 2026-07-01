using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

/// <summary>Lời mời phỏng vấn candidate nhận được từ HR (SCRUM-295): xem danh sách, chấp nhận hoặc từ chối.</summary>
[ApiController]
[Route("api/candidate/invitations")]
[Authorize(Roles = "Candidate")]
public class CandidateInvitationsController : ControllerBase
{
    private readonly ICandidateInvitationService _service;

    public CandidateInvitationsController(ICandidateInvitationService service)
    {
        _service = service;
    }

    /// <summary>Danh sách lời mời của Candidate hiện tại (mới nhất trước) — kèm tên/logo công ty, tên bộ câu hỏi, lời nhắn HR và trạng thái phản hồi.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var result = await _service.ListAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Chấp nhận lời mời (PENDING → ACCEPTED). 409 nếu đã phản hồi trước đó.</summary>
    /// <param name="id">Id lời mời.</param>
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id)
    {
        var result = await _service.AcceptAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Từ chối lời mời (PENDING → REJECTED). 409 nếu đã phản hồi trước đó.</summary>
    /// <param name="id">Id lời mời.</param>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id)
    {
        var result = await _service.RejectAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }
}
