using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.DTOs.Recommendation;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

/// <summary>Quản lý bộ câu hỏi của HR (draft đã lưu từ session sinh câu hỏi): xem, sửa từng câu, publish/unpublish lên marketplace. Yêu cầu role HR.</summary>
[ApiController]
[Route("api/hr/question-sets")]
[Authorize(Roles = "HR")]
public class HrQuestionSetsController : ControllerBase
{
    private readonly IQuestionSetService _service;
    private readonly IHrBookmarkService _bookmarkService;
    private readonly IQuestionSetFeedbackService _feedbackService;
    private readonly IRecommendationService _recommendationService;
    private readonly IQuestionSetJdFitService _jdFitService;

    public HrQuestionSetsController(
        IQuestionSetService service,
        IHrBookmarkService bookmarkService,
        IQuestionSetFeedbackService feedbackService,
        IRecommendationService recommendationService,
        IQuestionSetJdFitService jdFitService)
    {
        _service = service;
        _bookmarkService = bookmarkService;
        _feedbackService = feedbackService;
        _recommendationService = recommendationService;
        _jdFitService = jdFitService;
    }

    /// <summary>Danh sách tất cả bộ câu hỏi (draft/published) mà HR hiện tại sở hữu.</summary>
    /// <param name="query">Lọc theo jobId (session sinh câu hỏi nguồn) — bỏ trống để lấy tất cả.</param>
    [HttpGet]
    public async Task<IActionResult> ListQuestionSets([FromQuery] QuestionSetListQueryDto query)
    {
        var result = await _service.ListQuestionSetsAsync(GetCurrentUserId(), query);
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-397: tạo bộ câu hỏi DRAFT rỗng từ Question Builder (chưa cần Studio Generate).</summary>
    /// <param name="dto">title bắt buộc; description tùy chọn (lưu HrNote).</param>
    [HttpPost]
    public async Task<IActionResult> CreateManualDraft([FromBody] CreateManualDraftQuestionSetRequestDto dto)
    {
        var result = await _service.CreateManualDraftAsync(GetCurrentUserId(), dto);
        return SuccessResp.Created(result);
    }

    /// <summary>Chi tiết 1 bộ câu hỏi kèm toàn bộ câu hỏi (có sampleAnswer/evaluationCriteria — chỉ HR chủ sở hữu mới xem được).</summary>
    /// <param name="id">Id bộ câu hỏi.</param>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetQuestionSet(Guid id)
    {
        var result = await _service.GetQuestionSetAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Sửa nội dung 1 câu hỏi trong bộ (câu hỏi, loại, độ khó, skill, sample answer...). Không sửa được khi bộ đang PUBLISHED — phải unpublish trước.</summary>
    /// <param name="questionSetId">Id bộ câu hỏi.</param>
    /// <param name="questionId">Id câu hỏi cần sửa.</param>
    /// <param name="dto">Nội dung câu hỏi mới.</param>
    [HttpPut("{questionSetId:guid}/questions/{questionId:guid}")]
    public async Task<IActionResult> UpdateQuestion(
        Guid questionSetId, Guid questionId, [FromBody] UpdateQuestionRequestDto dto)
    {
        var result = await _service.UpdateQuestionAsync(questionSetId, questionId, GetCurrentUserId(), dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Thêm 1 câu hỏi mới vào bộ. Không thêm được khi bộ đang PUBLISHED — phải unpublish trước.</summary>
    /// <param name="questionSetId">Id bộ câu hỏi.</param>
    /// <param name="dto">Nội dung câu hỏi cần thêm.</param>
    [HttpPost("{questionSetId:guid}/questions")]
    public async Task<IActionResult> AddQuestion(Guid questionSetId, [FromBody] CreateQuestionRequestDto dto)
    {
        var result = await _service.AddQuestionAsync(questionSetId, GetCurrentUserId(), dto);
        return SuccessResp.Created(result);
    }

    /// <summary>Xóa 1 câu hỏi khỏi bộ (không xóa được câu cuối cùng). Không xóa được khi bộ đang PUBLISHED — phải unpublish trước.</summary>
    /// <param name="questionSetId">Id bộ câu hỏi.</param>
    /// <param name="questionId">Id câu hỏi cần xóa.</param>
    [HttpDelete("{questionSetId:guid}/questions/{questionId:guid}")]
    public async Task<IActionResult> DeleteQuestion(Guid questionSetId, Guid questionId)
    {
        await _service.DeleteQuestionAsync(questionSetId, questionId, GetCurrentUserId());
        return SuccessResp.NoContent();
    }

    /// <summary>Sắp xếp lại thứ tự câu hỏi trong bộ — truyền đủ danh sách (questionId, order) cho toàn bộ câu hỏi hiện có.</summary>
    /// <param name="questionSetId">Id bộ câu hỏi.</param>
    /// <param name="dto">Danh sách (questionId, order) cho toàn bộ câu hỏi.</param>
    [HttpPut("{questionSetId:guid}/questions/reorder")]
    public async Task<IActionResult> ReorderQuestions(
        Guid questionSetId, [FromBody] ReorderQuestionsRequestDto dto)
    {
        var result = await _service.ReorderQuestionsAsync(questionSetId, GetCurrentUserId(), dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Đặt giới hạn thời gian làm bài practice cho bộ câu hỏi (1–480 phút, truyền null để bỏ giới hạn). Candidate hết giờ sẽ bị tự động nộp bài. Không đổi được khi bộ đang PUBLISHED — phải unpublish trước.</summary>
    /// <param name="id">Id bộ câu hỏi.</param>
    /// <param name="dto">timeLimitMinutes: 1–480, hoặc null = không giới hạn.</param>
    [HttpPut("{id:guid}/time-limit")]
    public async Task<IActionResult> SetTimeLimit(Guid id, [FromBody] SetTimeLimitRequestDto dto)
    {
        var result = await _service.SetTimeLimitAsync(id, GetCurrentUserId(), dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Đổi tên bộ câu hỏi (SCRUM-330) — không ảnh hưởng trạng thái DRAFT/PUBLISHED hiện tại. Marketplace/danh sách sẽ phản ánh tên mới ngay.</summary>
    /// <param name="id">Id bộ câu hỏi.</param>
    /// <param name="dto">title: 1–500 ký tự, không được để trống.</param>
    [HttpPut("{id:guid}/title")]
    public async Task<IActionResult> RenameTitle(Guid id, [FromBody] RenameQuestionSetTitleRequestDto dto)
    {
        var result = await _service.RenameTitleAsync(id, GetCurrentUserId(), dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Danh sách candidate đã practice bộ câu hỏi này (SCRUM-326) — mọi trạng thái phiên (IN_PROGRESS/COMPLETED/ABANDONED), không lọc theo ngưỡng điểm như Recommendation. Chỉ HR chủ sở hữu xem được. Candidate đã tắt "Cho phép đề xuất hồ sơ cho HR" (AllowRecruiterRecommendation) sẽ không xuất hiện trong danh sách này.</summary>
    /// <param name="id">Id bộ câu hỏi.</param>
    [HttpGet("{id:guid}/practitioners")]
    public async Task<IActionResult> GetPractitioners(Guid id)
    {
        var result = await _service.GetPractitionersAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Mời ứng viên từ practitioners — tạo recommendation nếu chưa có, không yêu cầu điểm ≥ 70.</summary>
    [HttpPost("{id:guid}/practitioners/{candidateUserId:guid}/invite")]
    public async Task<IActionResult> InvitePractitioner(Guid id, Guid candidateUserId, [FromBody] InviteCandidateRequestDto? dto)
    {
        var result = await _recommendationService.InviteFromPractitionerAsync(
            id, candidateUserId, GetCurrentUserId(), dto ?? new InviteCandidateRequestDto());
        return SuccessResp.Created(result);
    }

    /// <summary>Danh sách feedback (rating + nhận xét) candidate đã gửi cho bộ câu hỏi này, kèm rating trung bình. Chỉ HR chủ sở hữu xem được.</summary>
    /// <param name="id">Id bộ câu hỏi.</param>
    /// <param name="query">page, pageSize.</param>
    [HttpGet("{id:guid}/feedback")]
    public async Task<IActionResult> GetFeedback(Guid id, [FromQuery] QuestionSetFeedbackQueryDto query)
    {
        var result = await _feedbackService.GetForOwnerAsync(id, GetCurrentUserId(), query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Bookmark/bỏ bookmark bộ câu hỏi của chính mình (SCRUM-324) — toggle: gọi lần 1 để bookmark, gọi lại để bỏ. Chỉ bookmark được set do chính HR này sở hữu (draft hoặc published).</summary>
    /// <param name="id">Id bộ câu hỏi.</param>
    [HttpPost("{id:guid}/bookmark")]
    public async Task<IActionResult> ToggleBookmark(Guid id)
    {
        var result = await _bookmarkService.ToggleAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Publish bộ câu hỏi lên marketplace cho Candidate xem (DRAFT → PUBLISHED). Yêu cầu tối thiểu 10 câu hỏi, chỉ HR chủ sở hữu mới publish được.</summary>
    /// <param name="id">Id bộ câu hỏi.</param>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var result = await _service.PublishAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Gỡ bộ câu hỏi khỏi marketplace (PUBLISHED → DRAFT) — Candidate sẽ không còn thấy/tìm được bộ này nữa.</summary>
    /// <param name="id">Id bộ câu hỏi.</param>
    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        var result = await _service.UnpublishAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-396: HR upload ảnh đính kèm câu hỏi trong bộ (History).</summary>
    [HttpPost("{questionSetId:guid}/questions/{questionId:guid}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadQuestionImage(
        Guid questionSetId,
        Guid questionId,
        [FromForm] QuestionSetQuestionImageUploadForm form)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest(new { Code = 400, Error = "File ảnh là bắt buộc." });

        await using var stream = form.File.OpenReadStream();
        var result = await _service.UploadQuestionImageAsync(
            questionSetId, questionId, GetCurrentUserId(),
            stream, form.File.FileName, form.File.ContentType, form.File.Length);
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-396: xóa ảnh đính kèm câu hỏi trong bộ.</summary>
    [HttpDelete("{questionSetId:guid}/questions/{questionId:guid}/image")]
    public async Task<IActionResult> DeleteQuestionImage(Guid questionSetId, Guid questionId)
    {
        var result = await _service.DeleteQuestionImageAsync(questionSetId, questionId, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-391: xuất Excel bộ câu hỏi (cần quyền CanExport trên gói subscription).</summary>
    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id)
    {
        var file = await _service.ExportExcelAsync(id, GetCurrentUserId());
        return File(
            file.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            file.FileName);
    }

    /// <summary>SCRUM-391: soft-delete bộ câu hỏi khỏi History (unpublish nếu đang PUBLISHED).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> SoftDelete(Guid id)
    {
        await _service.SoftDeleteAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(new { questionSetId = id, deleted = true });
    }

    /// <summary>Gắn/cập nhật JD bằng text — dùng cho JD Fit khi bộ chưa có JD.</summary>
    [HttpPut("{id:guid}/job-description")]
    public async Task<IActionResult> SetJobDescription(Guid id, [FromBody] UpdateQuestionSetJobDescriptionRequestDto dto)
    {
        var result = await _service.SetJobDescriptionFromTextAsync(id, GetCurrentUserId(), dto.JobDescription);
        return SuccessResp.Ok(result);
    }

    /// <summary>Gắn/cập nhật JD từ file PDF/DOCX/TXT (parse RAG).</summary>
    [HttpPost("{id:guid}/job-description")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadJobDescription(Guid id, [FromForm] QuestionSetJdUploadForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            throw new BadRequestException("Vui lòng chọn file Job Description.");

        await using var stream = form.File.OpenReadStream();
        var result = await _service.SetJobDescriptionFromFileAsync(
            id, GetCurrentUserId(), stream, form.File.FileName, ct);
        return SuccessResp.Ok(result);
    }

    /// <summary>Đọc bản đánh giá JD đã lưu — không gọi RAG, không trừ quota.</summary>
    [HttpGet("{id:guid}/jd-fit-review")]
    public async Task<IActionResult> GetJdFitReview(Guid id, CancellationToken ct)
    {
        var result = await _jdFitService.GetAsync(id, GetCurrentUserId(), ct);
        return SuccessResp.Ok(result);
    }

    /// <summary>Đánh giá bộ câu hỏi so với JD — nhận xét lời, upsert bảng 1–1, tính quota Ask AI.</summary>
    [HttpPost("{id:guid}/jd-fit-review")]
    public async Task<IActionResult> JdFitReview(Guid id, CancellationToken ct)
    {
        var result = await _jdFitService.ReviewAsync(id, GetCurrentUserId(), ct);
        return SuccessResp.Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("Không xác định được người dùng từ token đăng nhập.");

        return userId;
    }
}

/// <summary>SCRUM-396: form upload ảnh câu hỏi Question Set.</summary>
public sealed class QuestionSetQuestionImageUploadForm
{
    public IFormFile? File { get; set; }
}

public sealed class QuestionSetJdUploadForm
{
    public IFormFile? File { get; set; }
}
