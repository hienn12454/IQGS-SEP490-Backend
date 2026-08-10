using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

/// <summary>Marketplace bộ câu hỏi phỏng vấn — Candidate xem/tìm/lọc bộ câu hỏi các công ty đã publish, và bookmark bộ yêu thích.</summary>
[ApiController]
[Route("api/candidate/question-sets")]
public class CandidateQuestionSetsController : ControllerBase
{
    private readonly ICandidateQuestionSetService _service;
    private readonly ICandidateBookmarkService _bookmarkService;
    private readonly IQuestionSetFeedbackService _feedbackService;

    public CandidateQuestionSetsController(
        ICandidateQuestionSetService service,
        ICandidateBookmarkService bookmarkService,
        IQuestionSetFeedbackService feedbackService)
    {
        _service = service;
        _bookmarkService = bookmarkService;
        _feedbackService = feedbackService;
    }

    /// <summary>Danh sách bộ câu hỏi đã PUBLISHED kèm tên/logo công ty, phân trang. Hỗ trợ tìm theo keyword (tên bộ/công ty), lọc theo companyId/difficulty/skills.</summary>
    /// <remarks>Public — không cần đăng nhập, ai cũng gọi được.</remarks>
    /// <param name="query">page, pageSize, keyword, companyId, difficulty, skills (có thể truyền nhiều lần).</param>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] CandidateQuestionSetListQueryDto query)
    {
        var result = await _service.ListPublishedAsync(query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Chi tiết 1 bộ câu hỏi đã PUBLISHED kèm toàn bộ danh sách câu hỏi — dùng để candidate xem trước khi bắt đầu luyện tập.</summary>
    /// <remarks>Yêu cầu đăng nhập role Candidate. KHÔNG trả sampleAnswer/evaluationCriteria (giữ bí mật đáp án mẫu). 404 nếu bộ không tồn tại hoặc chưa publish.</remarks>
    /// <param name="id">Id bộ câu hỏi.</param>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetPublishedByIdAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Toggle bookmark 1 bộ câu hỏi — gọi lần 1 để thêm vào danh sách yêu thích, gọi lại lần 2 để bỏ (xem lại danh sách qua GET /api/candidate/bookmarks).</summary>
    /// <remarks>Yêu cầu đăng nhập role Candidate. Chỉ bookmark được bộ đang PUBLISHED — 404 nếu chưa publish/không tồn tại.</remarks>
    /// <param name="id">Id bộ câu hỏi.</param>
    [HttpPost("{id:guid}/bookmark")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> ToggleBookmark(Guid id)
    {
        var result = await _bookmarkService.ToggleAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Danh sách feedback (rating + nhận xét) candidate khác đã gửi cho bộ câu hỏi này, kèm rating trung bình.</summary>
    /// <remarks>Public — không cần đăng nhập. 404 nếu bộ không tồn tại hoặc chưa publish.</remarks>
    /// <param name="id">Id bộ câu hỏi.</param>
    /// <param name="query">page, pageSize.</param>
    [HttpGet("{id:guid}/feedback")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeedback(Guid id, [FromQuery] QuestionSetFeedbackQueryDto query)
    {
        var result = await _feedbackService.GetPublicAsync(id, query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Feedback của chính candidate hiện tại cho bộ câu hỏi này — null nếu chưa gửi. Dùng để prefill form sửa.</summary>
    /// <param name="id">Id bộ câu hỏi.</param>
    [HttpGet("{id:guid}/feedback/me")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> GetMyFeedback(Guid id)
    {
        var result = await _feedbackService.GetMineAsync(User.GetUserId(), id);
        return SuccessResp.Ok(result);
    }

    /// <summary>Gửi/sửa feedback (rating 0-5 + nhận xét) cho bộ câu hỏi — yêu cầu đã có phiên luyện tập hoàn thành trên bộ này. Gọi lại sẽ update đè feedback cũ.</summary>
    /// <param name="id">Id bộ câu hỏi.</param>
    /// <param name="dto">rating (0-5, bắt buộc), comment (tùy chọn, tối đa 2000 ký tự).</param>
    [HttpPost("{id:guid}/feedback")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> SubmitFeedback(Guid id, [FromBody] SubmitQuestionSetFeedbackDto dto)
    {
        var result = await _feedbackService.SubmitAsync(User.GetUserId(), id, dto);
        return SuccessResp.Ok(result);
    }
}
