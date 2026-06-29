using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

[ApiController]
[Route("api/candidate/question-sets")]
public class CandidateQuestionSetsController : ControllerBase
{
    private readonly ICandidateQuestionSetService _service;
    private readonly ICandidateBookmarkService _bookmarkService;

    public CandidateQuestionSetsController(
        ICandidateQuestionSetService service, ICandidateBookmarkService bookmarkService)
    {
        _service = service;
        _bookmarkService = bookmarkService;
    }

    /// <summary>Danh sách bộ câu hỏi đã PUBLISHED, kèm thông tin công ty, phân trang. Public — không yêu cầu đăng nhập (AC-05 SCRUM-267).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] CandidateQuestionSetListQueryDto query)
    {
        var result = await _service.ListPublishedAsync(query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Chi tiết 1 bộ PUBLISHED kèm danh sách câu hỏi. Yêu cầu đăng nhập role Candidate (AC-05 SCRUM-269), không trả sampleAnswer/evaluationCriteria.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetPublishedByIdAsync(id);
        return SuccessResp.Ok(result);
    }

    /// <summary>Toggle bookmark bộ câu hỏi (thêm nếu chưa có, bỏ nếu đã có). Chỉ bộ PUBLISHED (SCRUM-275).</summary>
    [HttpPost("{id:guid}/bookmark")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> ToggleBookmark(Guid id)
    {
        var result = await _bookmarkService.ToggleAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }
}
