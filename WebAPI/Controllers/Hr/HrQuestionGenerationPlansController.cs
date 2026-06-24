using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

[ApiController]
[Route("api/hr/question-generation-plans")]
[Authorize(Roles = "HR")]
public class HrQuestionGenerationPlansController : ControllerBase
{
    private readonly IQuestionGenerationJobService _service;

    public HrQuestionGenerationPlansController(IQuestionGenerationJobService service)
    {
        _service = service;
    }

    /// <summary>Danh sách plan đã tạo của HR — phân trang, lọc status/ngày.</summary>
    [HttpGet]
    public async Task<IActionResult> ListPlans([FromQuery] QuestionGenerationListQueryDto query)
    {
        var result = await _service.ListPlansAsync(GetCurrentUserId(), query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Chi tiết plan theo jobId — full PlanJson + input gốc HR.</summary>
    [HttpGet("{jobId:guid}")]
    public async Task<IActionResult> GetPlan(Guid jobId)
    {
        var result = await _service.GetPlanAsync(jobId, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Tải file Excel chi tiết các câu hỏi đã sinh (chỉ khi COMPLETED).</summary>
    [HttpGet("{jobId:guid}/questions/export")]
    public async Task<IActionResult> ExportQuestions(Guid jobId)
    {
        var result = await _service.ExportPlanQuestionsExcelAsync(jobId, GetCurrentUserId());
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>Xóa session plan (job + plan + câu hỏi đã sinh).</summary>
    [HttpDelete("{jobId:guid}")]
    public async Task<IActionResult> DeletePlan(Guid jobId)
    {
        await _service.DeletePlanAsync(jobId, GetCurrentUserId());
        return SuccessResp.NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdStr!);
    }
}
