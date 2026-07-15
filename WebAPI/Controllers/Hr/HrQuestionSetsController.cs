using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
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

    public HrQuestionSetsController(IQuestionSetService service)
    {
        _service = service;
    }

    /// <summary>Danh sách tất cả bộ câu hỏi (draft/published) mà HR hiện tại sở hữu.</summary>
    /// <param name="query">Lọc theo jobId (session sinh câu hỏi nguồn) — bỏ trống để lấy tất cả.</param>
    [HttpGet]
    public async Task<IActionResult> ListQuestionSets([FromQuery] QuestionSetListQueryDto query)
    {
        var result = await _service.ListQuestionSetsAsync(GetCurrentUserId(), query);
        return SuccessResp.Ok(result);
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

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("Không xác định được người dùng từ token đăng nhập.");

        return userId;
    }
}
