using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.DTOs.QuestionSet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

/// <summary>
/// Generate V1 API đã retired (Studio generate-v2). Mọi endpoint trả 410 Gone.
/// Bảng V1 sẽ drop ở migration Phase 4.
/// </summary>
[ApiController]
[Route("api/hr/question-generation-jobs")]
[Authorize(Roles = "HR")]
public class HrQuestionGenerationJobsController : ControllerBase
{
    private const string GoneMessage =
        "Generate V1 đã ngừng. Dùng Studio: /hr/generate-v2 và /api/studio/projects.";

    private static IActionResult Gone()
        => new ObjectResult(new { error = "V1_GENERATE_RETIRED", message = GoneMessage })
        {
            StatusCode = StatusCodes.Status410Gone
        };

    [HttpPost("plan")]
    public IActionResult CreatePlanJob([FromBody] CreatePlanJobRequestDto? dto, CancellationToken ct) => Gone();

    [HttpPost("plan/upload")]
    public IActionResult CreatePlanJobFromUpload() => Gone();

    [HttpGet]
    public IActionResult List() => Gone();

    [HttpGet("{jobId:guid}")]
    public IActionResult Get(Guid jobId) => Gone();

    [HttpPost("{jobId:guid}/approve-plan")]
    public IActionResult ApprovePlan(Guid jobId) => Gone();

    [HttpPost("{jobId:guid}/retry-plan")]
    public IActionResult RetryPlan(Guid jobId) => Gone();

    [HttpPost("{jobId:guid}/retry-questions")]
    public IActionResult RetryQuestions(Guid jobId) => Gone();

    [HttpPut("{jobId:guid}/plan")]
    public IActionResult UpdatePlan(Guid jobId) => Gone();

    [HttpPut("{jobId:guid}/input")]
    public IActionResult UpdateInput(Guid jobId) => Gone();

    [HttpPut("{jobId:guid}/input/upload")]
    public IActionResult UpdateInputUpload(Guid jobId) => Gone();

    [HttpGet("{jobId:guid}/questions")]
    public IActionResult GetQuestions(Guid jobId) => Gone();

    [HttpPost("{jobId:guid}/questions")]
    public IActionResult AddQuestion(Guid jobId) => Gone();

    [HttpPut("{jobId:guid}/questions/{questionId:guid}")]
    public IActionResult UpdateQuestion(Guid jobId, Guid questionId) => Gone();

    [HttpDelete("{jobId:guid}/questions/{questionId:guid}")]
    public IActionResult DeleteQuestion(Guid jobId, Guid questionId) => Gone();

    [HttpPut("{jobId:guid}/questions/reorder")]
    public IActionResult Reorder(Guid jobId) => Gone();

    [HttpPost("{jobId:guid}/save-draft")]
    public IActionResult SaveDraft(Guid jobId) => Gone();

    [HttpPost("{jobId:guid}/questions/{questionId:guid}/ask-ai")]
    public IActionResult AskAi(Guid jobId, Guid questionId) => Gone();

    [HttpGet("{jobId:guid}/questions/{questionId:guid}/ai-chat")]
    public IActionResult GetAiChat(Guid jobId, Guid questionId) => Gone();

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(sub!);
    }
}
