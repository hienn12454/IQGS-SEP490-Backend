using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Studio;

[ApiController]
[Authorize(Roles = "HR")]
[Route("api/studio/projects")]
public sealed class StudioInterviewProjectsController(IInterviewProjectService projectService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudioProjectRequest request, CancellationToken ct)
    {
        var result = await projectService.CreateAsync(GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetMyProjects(CancellationToken ct)
    {
        var result = await projectService.ListByOwnerAsync(GetUserId(), ct);
        return Ok(result);
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
    {
        return Ok(await projectService.GetAsync(projectId, GetUserId(), ct));
    }

    [HttpPut("{projectId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, [FromBody] UpdateStudioProjectRequest request, CancellationToken ct)
    {
        return Ok(await projectService.UpdateAsync(projectId, GetUserId(), request, ct));
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken ct)
    {
        await projectService.DeleteAsync(projectId, GetUserId(), ct);
        return NoContent();
    }

    [HttpPost("{projectId:guid}/save")]
    [HttpPost("{projectId:guid}/save-draft")] // alias FE cũ 1 sprint
    public async Task<IActionResult> Save(Guid projectId, CancellationToken ct)
    {
        var result = await projectService.SaveQuestionSetAsync(projectId, GetUserId(), ct);
        return Ok(result);
    }

    [HttpPost("{projectId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid projectId, CancellationToken ct)
    {
        await projectService.PublishFromProjectAsync(projectId, GetUserId(), ct);
        return Ok(new { message = "Đã publish." });
    }

    [HttpPost("{projectId:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid projectId, CancellationToken ct)
    {
        var result = await projectService.UnpublishFromProjectAsync(projectId, GetUserId(), ct);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(sub!);
    }
}
