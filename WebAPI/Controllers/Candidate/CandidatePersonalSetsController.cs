using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

[ApiController]
[Route("api/candidate/personal-sets")]
[Authorize(Roles = "Candidate")]
public class CandidatePersonalSetsController : ControllerBase
{
    private readonly ICandidatePersonalSetService _service;

    public CandidatePersonalSetsController(ICandidatePersonalSetService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> ListMine()
    {
        var result = await _service.ListMineAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var result = await _service.GetJobAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateFromText([FromBody] CreatePersonalSetFromTextDto dto, CancellationToken ct)
    {
        var result = await _service.CreateFromTextAsync(User.GetUserId(), dto, ct);
        return SuccessResp.Ok(result);
    }

    [HttpPost("from-file")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateFromFile(IFormFile file, [FromForm] int numberOfQuestions = 10, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { Code = 400, Error = "File là bắt buộc." });

        await using var stream = file.OpenReadStream();
        var result = await _service.CreateFromFileAsync(User.GetUserId(), stream, file.FileName, numberOfQuestions, ct);
        return SuccessResp.Ok(result);
    }
}
