using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

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

    /// <summary>Danh sách tất cả question set của HR — mỗi item có questionSetId và jobId.</summary>
    [HttpGet]
    public async Task<IActionResult> ListQuestionSets([FromQuery] QuestionSetListQueryDto query)
    {
        var result = await _service.ListQuestionSetsAsync(GetCurrentUserId(), query);
        return SuccessResp.Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetQuestionSet(Guid id)
    {
        var result = await _service.GetQuestionSetAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    [HttpPut("{questionSetId:guid}/questions/{questionId:guid}")]
    public async Task<IActionResult> UpdateQuestion(
        Guid questionSetId, Guid questionId, [FromBody] UpdateQuestionRequestDto dto)
    {
        var result = await _service.UpdateQuestionAsync(questionSetId, questionId, GetCurrentUserId(), dto);
        return SuccessResp.Ok(result);
    }

    [HttpPost("{questionSetId:guid}/questions")]
    public async Task<IActionResult> AddQuestion(Guid questionSetId, [FromBody] CreateQuestionRequestDto dto)
    {
        var result = await _service.AddQuestionAsync(questionSetId, GetCurrentUserId(), dto);
        return SuccessResp.Created(result);
    }

    [HttpDelete("{questionSetId:guid}/questions/{questionId:guid}")]
    public async Task<IActionResult> DeleteQuestion(Guid questionSetId, Guid questionId)
    {
        await _service.DeleteQuestionAsync(questionSetId, questionId, GetCurrentUserId());
        return SuccessResp.NoContent();
    }

    [HttpPut("{questionSetId:guid}/questions/reorder")]
    public async Task<IActionResult> ReorderQuestions(
        Guid questionSetId, [FromBody] ReorderQuestionsRequestDto dto)
    {
        var result = await _service.ReorderQuestionsAsync(questionSetId, GetCurrentUserId(), dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>Publish bộ câu hỏi lên marketplace (DRAFT → PUBLISHED). Yêu cầu tối thiểu 10 câu hỏi.</summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var result = await _service.PublishAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Gỡ bộ câu hỏi khỏi marketplace (PUBLISHED → DRAFT).</summary>
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
