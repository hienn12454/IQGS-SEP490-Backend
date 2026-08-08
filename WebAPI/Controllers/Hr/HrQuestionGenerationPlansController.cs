using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Hr;

/// <summary>Plan API V1 retired — dùng Studio.</summary>
[ApiController]
[Route("api/hr/question-generation-plans")]
[Authorize(Roles = "HR")]
public class HrQuestionGenerationPlansController : ControllerBase
{
    private static IActionResult Gone()
        => new ObjectResult(new
        {
            error = "V1_GENERATE_RETIRED",
            message = "Generate V1 đã ngừng. Dùng Studio: /hr/generate-v2 và /api/studio/projects."
        })
        {
            StatusCode = StatusCodes.Status410Gone
        };

    [HttpGet]
    public IActionResult ListPlans() => Gone();

    [HttpGet("{jobId:guid}")]
    public IActionResult GetPlan(Guid jobId) => Gone();

    [HttpGet("{jobId:guid}/questions/export")]
    public IActionResult ExportQuestions(Guid jobId) => Gone();

    [HttpDelete("{jobId:guid}")]
    public IActionResult DeletePlan(Guid jobId) => Gone();
}
