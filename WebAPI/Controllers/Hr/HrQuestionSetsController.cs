using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers.Hr;

[ApiController]
[Route("api/hr/question-sets")]
[Authorize(Roles = "HR")]
public class HrQuestionSetsController : ControllerBase
{
    private readonly IQuestionSetService _service;

    public HrQuestionSetsController(IQuestionSetService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetQuestionSet(Guid id)
    {
        var result = await _service.GetQuestionSetAsync(id, GetCurrentUserId());
        return SuccessResp.Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdStr!);
    }
}
