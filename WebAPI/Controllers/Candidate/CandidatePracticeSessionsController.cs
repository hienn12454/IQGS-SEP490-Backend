using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

/// <summary>Vòng đời phiên luyện tập phỏng vấn của Candidate: bắt đầu/tiếp tục, trả lời từng câu, hoàn thành, xem lịch sử + thống kê + AI feedback.</summary>
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

    /// <summary>Bắt đầu phiên luyện tập mới từ 1 bộ câu hỏi PUBLISHED. Nếu đang có phiên IN_PROGRESS dở dang cho cùng bộ câu hỏi này, trả về phiên đó thay vì tạo mới (resume tự động).</summary>
    /// <remarks>404 nếu questionSetId không tồn tại hoặc chưa publish.</remarks>
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartPracticeSessionDto dto)
    {
        var result = await _service.StartAsync(dto.QuestionSetId, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Danh sách phiên luyện tập của Candidate hiện tại — mặc định chỉ lấy COMPLETED, truyền status=IN_PROGRESS để lấy phiên đang làm dở. Hỗ trợ tìm theo keyword (tên bộ/công ty) và lọc theo ngày.</summary>
    /// <param name="query">status (mặc định COMPLETED), questionSetId, keyword, page, pageSize, và lọc ngày: fromDate/toDate (khoảng cụ thể), hoặc year+month (1 tháng), hoặc chỉ year (cả năm).</param>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PracticeSessionListQueryDto query)
    {
        var result = await _service.ListAsync(User.GetUserId(), query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Thống kê luyện tập của Candidate hiện tại: tổng số phiên COMPLETED, điểm trung bình, điểm cao nhất, điểm bài gần nhất, tổng thời gian luyện tập.</summary>
    /// <param name="query">Lọc theo ngày: fromDate/toDate (khoảng cụ thể), hoặc year+month (1 tháng), hoặc chỉ year (cả năm) — bỏ trống để tính toàn bộ thời gian.</param>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] PracticeSessionStatsQueryDto query)
    {
        var result = await _service.GetStatsAsync(User.GetUserId(), query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Chi tiết 1 phiên luyện tập (đang chạy hoặc đã xong) — danh sách câu hỏi kèm câu trả lời candidate đã submit cho từng câu (null nếu chưa trả lời).</summary>
    /// <remarks>Chỉ chủ phiên mới xem được — 403 nếu là candidate khác, 404 nếu không tồn tại.</remarks>
    /// <param name="id">Id phiên luyện tập.</param>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Lấy toàn bộ AI feedback của phiên (score từng câu + overall) — SCRUM-282.</summary>
    /// <remarks>Chỉ chủ phiên xem được. FE dùng cho màn result `/jobseeker/practice/[id]/result`.</remarks>
    /// <param name="id">Id phiên luyện tập.</param>
    [HttpGet("{id:guid}/feedback")]
    public async Task<IActionResult> GetFeedback(Guid id)
    {
        var result = await _service.GetFeedbackAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Lưu câu trả lời cho 1 câu hỏi, sau đó gọi RAG evaluate sync và lưu ai_feedbacks (SCRUM-282).</summary>
    /// <remarks>
    /// Upsert answer. Câu trả lời trống / quá ngắn / "không biết" / spam → score=0, evaluationStatus=Succeeded, không gọi AI.
    /// Nếu RAG timeout/lỗi → answer vẫn được lưu, evaluationStatus=Failed (HTTP 200).
    /// questionId phải thuộc đúng bộ câu hỏi của phiên này, nếu không → 400.
    /// </remarks>
    /// <param name="id">Id phiên luyện tập.</param>
    /// <param name="dto">questionId và answerText.</param>
    [HttpPost("{id:guid}/answers")]
    public async Task<IActionResult> SubmitAnswer(Guid id, [FromBody] SubmitAnswerDto dto)
    {
        var result = await _service.SubmitAnswerAsync(id, User.GetUserId(), dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Đánh dấu phiên luyện tập đã hoàn thành (IN_PROGRESS → COMPLETED), tính overall_score = tổng điểm Succeeded / tổng số câu trong bộ (câu chưa làm = 0), làm tròn 2 chữ số thập phân.</summary>
    /// <remarks>Chỉ hoàn thành được phiên đang IN_PROGRESS — 400 nếu phiên đã COMPLETED/ABANDONED. Ví dụ bộ 30 câu, 1 câu 95 điểm → overall ≈ 3.17.</remarks>
    /// <param name="id">Id phiên luyện tập.</param>
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var result = await _service.CompleteAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Candidate chủ động huỷ bỏ phiên đang làm dở (IN_PROGRESS → ABANDONED) — dùng khi muốn thoát mà không tính là đã hoàn thành.</summary>
    /// <param name="id">Id phiên luyện tập.</param>
    [HttpPost("{id:guid}/abandon")]
    public async Task<IActionResult> Abandon(Guid id)
    {
        var result = await _service.AbandonAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }
}
