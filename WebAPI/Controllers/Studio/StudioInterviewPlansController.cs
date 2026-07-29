using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Studio;

[ApiController]
[Authorize(Roles = "HR")]
[Route("api/studio/projects/{projectId:guid}/plans")]
public sealed class StudioInterviewPlansController(IInterviewPlanService planService) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(Guid projectId, CancellationToken ct)
    {
        var result = await planService.GenerateInitialAsync(projectId, GetUserId(), ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, CancellationToken ct)
    {
        var result = await planService.ListAsync(projectId, GetUserId(), ct);
        return Ok(result);
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current(Guid projectId, CancellationToken ct)
    {
        var result = await planService.GetCurrentAsync(projectId, GetUserId(), ct);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("{planId:guid}")]
    public async Task<IActionResult> Detail(Guid projectId, Guid planId, CancellationToken ct)
        => Ok(await planService.GetDetailAsync(projectId, planId, GetUserId(), ct));

    [HttpPost("{planId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid projectId, Guid planId, [FromBody] ApprovePlanRequest request, CancellationToken ct)
    {
        var result = await planService.ApproveAsync(projectId, planId, GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpPost("{planId:guid}/refine")]
    public async Task<IActionResult> Refine(Guid projectId, Guid planId, [FromBody] StudioRefinePlanRequest request, CancellationToken ct)
    {
        var result = await planService.RefineAsync(projectId, planId, GetUserId(), request.Instruction, ct);
        return Ok(result);
    }

    /// <summary>SCRUM-370: Áp dụng quick controls Studio → refine plan (không cần chat).</summary>
    [HttpPost("{planId:guid}/apply-settings")]
    public async Task<IActionResult> ApplySettings(Guid projectId, Guid planId, [FromBody] ApplyPlanSettingsRequest request, CancellationToken ct)
    {
        var result = await planService.ApplySettingsAsync(projectId, planId, GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpGet("{planId:guid}/history")]
    public async Task<IActionResult> History(Guid projectId, Guid planId, CancellationToken ct)
    {
        var result = await planService.GetApprovalHistoryAsync(projectId, planId, GetUserId(), ct);
        return Ok(result);
    }

    [HttpPost("{planId:guid}/submit-for-approval")]
    public async Task<IActionResult> SubmitForApproval(Guid projectId, Guid planId, CancellationToken ct)
        => Ok(await planService.SubmitForApprovalAsync(projectId, planId, GetUserId(), ct));

    [HttpPost("{planId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid projectId, Guid planId, [FromBody] RejectPlanRequest request, CancellationToken ct)
        => Ok(await planService.RejectAsync(projectId, planId, GetUserId(), request, ct));

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(sub!);
    }

    public sealed record StudioRefinePlanRequest(string Instruction);
}
