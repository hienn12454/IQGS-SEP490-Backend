using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebAPI.Helpers;

namespace WebAPI.Controllers.Hr;

[ApiController]
[Route("api/hr/question-generation-jobs")]
[Authorize(Roles = "HR")]
public class HrQuestionGenerationJobsController : ControllerBase
{
    private readonly IQuestionGenerationJobService _service;
    private readonly IQuestionSetService _questionSetService;

    public HrQuestionGenerationJobsController(IQuestionGenerationJobService service, IQuestionSetService questionSetService)
    {
        _service = service;
        _questionSetService = questionSetService;
    }

    [HttpPost("plan")]
    public async Task<IActionResult> CreatePlanJob([FromBody] CreatePlanJobRequestDto dto, CancellationToken ct)
    {
        var result = await _service.CreatePlanJobAsync(GetCurrentUserId(), dto, ct);
        return SuccessResp.Accepted(result);
    }

    [HttpPost("plan/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreatePlanJobFromUpload(
        [FromForm] CreatePlanJobUploadForm form,
        CancellationToken ct)
    {
        Stream? fileStream = null;
        if (form.File is { Length: > 0 })
            fileStream = form.File.OpenReadStream();

        var questionTypes = FormFieldListParser.Parse(form.QuestionTypes);
        var skills = FormFieldListParser.Parse(form.Skills);

        var result = await _service.CreatePlanJobFromUploadAsync(
            GetCurrentUserId(),
            form.JobDescription,
            form.HrNote,
            fileStream,
            form.File?.FileName,
            form.File?.Length ?? 0,
            form.NumberOfQuestions,
            form.Difficulty,
            questionTypes,
            skills,
            ct);

        return SuccessResp.Accepted(result);
    }

    [HttpGet]
    public async Task<IActionResult> ListJobs([FromQuery] QuestionGenerationListQueryDto query)
    {
        var result = await _service.ListJobsAsync(GetCurrentUserId(), query);
        return SuccessResp.Ok(result);
    }

    [HttpGet("{jobId:guid}")]
    public async Task<IActionResult> GetJob(Guid jobId)
    {
        var result = await _service.GetJobAsync(jobId, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    [HttpPut("{jobId:guid}/plan")]
    public async Task<IActionResult> UpdatePlan(Guid jobId, [FromBody] UpdatePlanRequestDto dto)
    {
        var result = await _service.UpdatePlanAsync(jobId, GetCurrentUserId(), dto);
        return SuccessResp.Ok(result);
    }

    [HttpPost("{jobId:guid}/approve-plan")]
    public async Task<IActionResult> ApprovePlan(Guid jobId)
    {
        var result = await _service.ApprovePlanAsync(jobId, GetCurrentUserId());
        return SuccessResp.Accepted(new { jobId = result.JobId, status = result.Status });
    }

    [HttpGet("{jobId:guid}/questions")]
    public async Task<IActionResult> GetQuestions(Guid jobId)
    {
        var result = await _service.GetQuestionsAsync(jobId, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }


    [HttpPut("{jobId:guid}/questions/{questionId:guid}")]
    public async Task<IActionResult> UpdateQuestion(
        Guid jobId, Guid questionId, [FromBody] UpdateQuestionRequestDto dto)
    {
        var result = await _service.UpdateQuestionAsync(jobId, questionId, GetCurrentUserId(), dto);
        return SuccessResp.Ok(result);
    }

    [HttpPost("{jobId:guid}/questions")]
    public async Task<IActionResult> AddQuestion(Guid jobId, [FromBody] CreateQuestionRequestDto dto)
    {
        var result = await _service.AddQuestionAsync(jobId, GetCurrentUserId(), dto);
        return SuccessResp.Created(result);
    }

    [HttpDelete("{jobId:guid}/questions/{questionId:guid}")]
    public async Task<IActionResult> DeleteQuestion(Guid jobId, Guid questionId)
    {
        await _service.DeleteQuestionAsync(jobId, questionId, GetCurrentUserId());
        return SuccessResp.NoContent();
    }

    [HttpPut("{jobId:guid}/questions/reorder")]
    public async Task<IActionResult> ReorderQuestions(
        Guid jobId, [FromBody] ReorderQuestionsRequestDto dto)
    {
        var result = await _service.ReorderQuestionsAsync(jobId, GetCurrentUserId(), dto);
        return SuccessResp.Ok(result);
    }

    [HttpPost("{jobId:guid}/save-draft")]
    public async Task<IActionResult> SaveDraft(Guid jobId)
    {
        var result = await _questionSetService.SaveDraftFromJobAsync(jobId, GetCurrentUserId());
        return SuccessResp.Created(result);
    }
    [HttpPost("{jobId:guid}/retry-plan")]
    public async Task<IActionResult> RetryPlan(Guid jobId)
    {
        var result = await _service.RetryPlanAsync(jobId, GetCurrentUserId());
        return SuccessResp.Accepted(new { jobId = result.JobId, status = result.Status });
    }

    [HttpPost("{jobId:guid}/retry-questions")]
    public async Task<IActionResult> RetryQuestions(Guid jobId)
    {
        var result = await _service.RetryQuestionsAsync(jobId, GetCurrentUserId());
        return SuccessResp.Accepted(new { jobId = result.JobId, status = result.Status });
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdStr!);
    }
}
