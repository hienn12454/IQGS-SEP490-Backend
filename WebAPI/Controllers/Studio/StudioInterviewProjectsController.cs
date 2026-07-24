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

    [HttpPost("{projectId:guid}/save-draft")]
    public async Task<IActionResult> SaveDraft(Guid projectId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, GetUserId(), requireEdit: true, ct);
        return Ok(new { message = "Đã lưu draft." });
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(sub!);
    }
}
