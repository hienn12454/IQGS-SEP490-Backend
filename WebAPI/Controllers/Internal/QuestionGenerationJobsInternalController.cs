using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Internal;

/// <summary>
/// Callback nội bộ từ RAG sau khi xử lý async sinh plan/câu hỏi.
/// </summary>
[ApiController]
[Route("internal/question-generation-jobs")]
[Tags("Internal — RAG callback (không dùng)")]
public class QuestionGenerationJobsInternalController : ControllerBase
{
    private readonly IQuestionGenerationJobInternalService _service;

    public QuestionGenerationJobsInternalController(IQuestionGenerationJobInternalService service)
    {
        _service = service;
    }

    /// <summary>
    /// RAG PATCH kết quả PLAN hoặc QUESTIONS sau background task.
    /// </summary>
    [HttpPatch("{jobId:guid}/generation-result")]
    public async Task<IActionResult> UpdateGenerationResult(
        Guid jobId,
        [FromBody] QuestionGenerationCallbackDto dto)
    {
        var result = await _service.UpdateGenerationResultAsync(jobId, dto);
        return SuccessResp.Ok(result);
    }
}
