using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

[ApiController]
[Route("api/candidate/practice-sessions")]
[Authorize(Roles = "Candidate")]
public class CandidatePracticeSessionsController : ControllerBase
{
    private readonly ICandidatePracticeSessionService _service;

    public CandidatePracticeSessionsController(ICandidatePracticeSessionService service)
    {
        _service = service;
    }

    /// <summary>Tạo phiên luyện tập mới, hoặc trả về phiên IN_PROGRESS đã có cho cùng bộ câu hỏi (SCRUM-277/298).</summary>
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartPracticeSessionDto dto)
    {
        var result = await _service.StartAsync(dto.QuestionSetId, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Danh sách phiên luyện tập của candidate hiện tại — mặc định COMPLETED, hỗ trợ filter status/keyword/ngày (SCRUM-284/298).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PracticeSessionListQueryDto query)
    {
        var result = await _service.ListAsync(User.GetUserId(), query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Thống kê: tổng số phiên, điểm trung bình, điểm cao nhất, tổng thời gian — hỗ trợ lọc theo ngày/tháng/năm (SCRUM-284).</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] PracticeSessionStatsQueryDto query)
    {
        var result = await _service.GetStatsAsync(User.GetUserId(), query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Lấy phiên luyện tập đang chạy hoặc đã hoàn thành — câu hỏi + câu trả lời đã submit (SCRUM-277/298).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Lưu/update câu trả lời cho 1 câu hỏi trong phiên (upsert) — SCRUM-278.</summary>
    [HttpPost("{id:guid}/answers")]
    public async Task<IActionResult> SubmitAnswer(Guid id, [FromBody] SubmitAnswerDto dto)
    {
        var result = await _service.SubmitAnswerAsync(id, User.GetUserId(), dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Hoàn thành phiên luyện tập — chuyển COMPLETED, set completed_at, tính overall_score (SCRUM-279).</summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var result = await _service.CompleteAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Candidate chủ động bỏ phiên đang làm dở (MVP optional — SCRUM-298).</summary>
    [HttpPost("{id:guid}/abandon")]
    public async Task<IActionResult> Abandon(Guid id)
    {
        var result = await _service.AbandonAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }
}
