using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using ApplicationLayer.Studio.Interfaces;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Internal;

/// <summary>
/// Callback nội bộ từ RAG sau khi xử lý async sinh plan/câu hỏi.
/// jobId có thể là QuestionGenerationJob (HR) hoặc QuestionGenerationRun (Studio SCRUM-371).
/// </summary>
[ApiController]
[Route("internal/question-generation-jobs")]
[Tags("Internal — RAG callback")]
public class QuestionGenerationJobsInternalController : ControllerBase
{
    private readonly IQuestionGenerationJobInternalService _hrJobService;
    private readonly IQuestionGenerationService _studioGenerationService;

    public QuestionGenerationJobsInternalController(
        IQuestionGenerationJobInternalService hrJobService,
        IQuestionGenerationService studioGenerationService)
    {
        _hrJobService = hrJobService;
        _studioGenerationService = studioGenerationService;
    }

    /// <summary>
    /// RAG PATCH kết quả PLAN hoặc QUESTIONS sau background task.
    /// </summary>
    [HttpPatch("{jobId:guid}/generation-result")]
    public async Task<IActionResult> UpdateGenerationResult(
        Guid jobId,
        [FromBody] QuestionGenerationCallbackDto dto,
        CancellationToken ct)
    {
        try
        {
            var result = await _hrJobService.UpdateGenerationResultAsync(jobId, dto);
            return SuccessResp.Ok(result);
        }
        catch (NotFoundException)
        {
            // SCRUM-371: cùng URL callback — jobId là Studio QuestionGenerationRun
            var applied = await _studioGenerationService.TryApplyRagCallbackAsync(jobId, dto, ct);
            if (!applied)
                throw;
            return SuccessResp.Ok(new { JobId = jobId, Applied = "StudioQuestionGenerationRun" });
        }
    }
}
