using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Studio;

[ApiController]
[Authorize(Roles = "HR")]
[Route("api/studio/projects/{projectId:guid}/job-description")]
public sealed class StudioJobDescriptionsController(IJobDescriptionService jdService) : ControllerBase
{
    [HttpPut]
    public async Task<IActionResult> Upsert(Guid projectId, [FromBody] UpsertJobDescriptionRequest request, CancellationToken ct)
    {
        await jdService.UpsertAsync(projectId, GetUserId(), request, ct);
        return Ok(new { message = "Đã lưu JD." });
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(Guid projectId, CancellationToken ct)
    {
        var result = await jdService.AnalyzeAsync(projectId, GetUserId(), ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
    {
        var content = await jdService.GetContentAsync(projectId, GetUserId(), ct);
        if (content is null)
            return NotFound(new { errorCode = "JOB_DESCRIPTION_NOT_FOUND", detail = "Chưa có JD." });
        return Ok(content);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(sub!);
    }
}
