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
    /// <summary>SCRUM-432: validate + classify + lưu; trả summary extract (FE không cần POST analyze lần 2).</summary>
    [HttpPut]
    public async Task<IActionResult> Upsert(Guid projectId, [FromBody] UpsertJobDescriptionRequest request, CancellationToken ct)
    {
        var summary = await jdService.UpsertAsync(projectId, GetUserId(), request, ct);
        return Ok(summary);
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(Guid projectId, CancellationToken ct)
    {
        var result = await jdService.AnalyzeAsync(projectId, GetUserId(), ct);
        return Ok(result);
    }

    /// <summary>SCRUM-416: HR sửa/xác nhận vị trí extract từ JD.</summary>
    [HttpPatch("position")]
    public async Task<IActionResult> UpdatePosition(
        Guid projectId,
        [FromBody] UpdateJobDescriptionPositionRequest request,
        CancellationToken ct)
    {
        var result = await jdService.UpdatePositionAsync(projectId, GetUserId(), request, ct);
        return Ok(result);
    }

    /// <summary>SCRUM-417: HR xác nhận Position + Level (+ Role) trước generate plan.</summary>
    [HttpPatch("metadata")]
    public async Task<IActionResult> UpdateMetadata(
        Guid projectId,
        [FromBody] UpdateJobDescriptionMetadataRequest request,
        CancellationToken ct)
    {
        var result = await jdService.UpdateMetadataAsync(projectId, GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpPost("recommend-configuration")]
    public async Task<IActionResult> RecommendConfiguration(
        Guid projectId,
        [FromBody] RecommendInterviewConfigurationRequestDto? request,
        CancellationToken ct)
    {
        var result = await jdService.RecommendConfigurationAsync(projectId, GetUserId(), request, ct);
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
