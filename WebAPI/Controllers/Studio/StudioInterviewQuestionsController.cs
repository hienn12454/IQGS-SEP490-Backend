using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Studio;

[ApiController]
[Authorize(Roles = "HR")]
[Route("api/studio/projects/{projectId:guid}/questions")]
public sealed class StudioInterviewQuestionsController(IQuestionGenerationService generationService) : ControllerBase
{
    /// <summary>SCRUM-371: nhận GenerationRun (Generating); câu hỏi về sau qua RAG callback.</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(Guid projectId, [FromBody] GenerateQuestionsRequest request, CancellationToken ct)
    {
        var result = await generationService.GenerateAsync(projectId, GetUserId(), request, ct);
        return Accepted(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, [FromQuery] Guid? planId, [FromQuery] Guid? sectionId, [FromQuery] string? difficulty, [FromQuery] string? type, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        Enum.TryParse<DomainLayer.Studio.Enums.QuestionDifficulty>(difficulty, true, out var parsedDifficulty);
        Enum.TryParse<DomainLayer.Studio.Enums.QuestionType>(type, true, out var parsedType);
        var request = new StudioQuestionListRequest(planId, sectionId, difficulty is null ? null : parsedDifficulty, type is null ? null : parsedType, search, page, pageSize);
        var result = await generationService.ListQuestionsAsync(projectId, GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpPut("{questionId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid questionId, [FromBody] UpdateQuestionRequest request, CancellationToken ct)
    {
        var result = await generationService.UpdateQuestionAsync(projectId, questionId, GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpDelete("{questionId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid questionId, CancellationToken ct)
    {
        await generationService.DeleteQuestionAsync(projectId, questionId, GetUserId(), ct);
        return NoContent();
    }

    [HttpPost("{questionId:guid}/regenerate")]
    public async Task<IActionResult> Regenerate(Guid projectId, Guid questionId, [FromBody] RegenerateQuestionRequest request, CancellationToken ct)
    {
        var result = await generationService.RegenerateQuestionAsync(projectId, questionId, GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpGet("/api/studio/projects/{projectId:guid}/question-generation-runs")]
    public async Task<IActionResult> Runs(Guid projectId, CancellationToken ct)
        => Ok(await generationService.ListRunsAsync(projectId, GetUserId(), ct));

    [HttpGet("/api/studio/projects/{projectId:guid}/question-generation-runs/{runId:guid}")]
    public async Task<IActionResult> RunDetail(Guid projectId, Guid runId, CancellationToken ct)
        => Ok(await generationService.GetRunAsync(projectId, runId, GetUserId(), ct));

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(sub!);
    }
}
