using ApplicationLayer.Studio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Studio;

[ApiController]
[Authorize(Roles = "HR")]
[Route("api/studio/projects/{projectId:guid}/chat")]
public sealed class StudioAiChatController(IAiChatService aiChatService) : ControllerBase
{
    public sealed record SendChatRequest(string Message);

    [HttpPost("messages")]
    public async Task Send(Guid projectId, [FromBody] SendChatRequest request, CancellationToken ct)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        await foreach (var evt in aiChatService.StreamMessageAsync(projectId, GetUserId(), request.Message, ct))
        {
            await Response.WriteAsync($"event: {evt.Event}\n", ct);
            await Response.WriteAsync($"data: {evt.Data}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    [HttpGet("session")]
    public async Task<IActionResult> GetSession(Guid projectId, CancellationToken ct)
        => Ok(await aiChatService.GetSessionAsync(projectId, GetUserId(), ct));

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages(Guid projectId, CancellationToken ct)
        => Ok(await aiChatService.GetMessagesAsync(projectId, GetUserId(), ct));

    [HttpGet("stream")]
    public async Task Stream(Guid projectId, [FromQuery] string message, CancellationToken ct)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        await foreach (var evt in aiChatService.StreamMessageAsync(projectId, GetUserId(), message, ct))
        {
            await Response.WriteAsync($"event: {evt.Event}\n", ct);
            await Response.WriteAsync($"data: {evt.Data}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(sub!);
    }
}
