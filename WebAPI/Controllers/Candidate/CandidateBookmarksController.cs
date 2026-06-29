using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

[ApiController]
[Route("api/candidate/bookmarks")]
[Authorize(Roles = "Candidate")]
public class CandidateBookmarksController : ControllerBase
{
    private readonly ICandidateBookmarkService _service;

    public CandidateBookmarksController(ICandidateBookmarkService service)
    {
        _service = service;
    }

    /// <summary>Danh sách bộ câu hỏi đã bookmark (SCRUM-275).</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var result = await _service.ListBookmarkedAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }
}
