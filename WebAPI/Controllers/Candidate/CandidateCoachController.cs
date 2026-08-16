using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

[ApiController]
[Route("api/candidate/coach")]
[Authorize(Roles = "Candidate")]
public class CandidateCoachController : ControllerBase
{
    private readonly ICandidatePersonalSetService _personalSets;
    private readonly ICandidateSkillPlanService _skillPlans;

    public CandidateCoachController(
        ICandidatePersonalSetService personalSets,
        ICandidateSkillPlanService skillPlans)
    {
        _personalSets = personalSets;
        _skillPlans = skillPlans;
    }

    [HttpPost("diagnostic")]
    public async Task<IActionResult> StartDiagnostic(CancellationToken ct)
    {
        var result = await _personalSets.StartCvDiagnosticAsync(User.GetUserId(), ct);
        return SuccessResp.Ok(result);
    }

    [HttpPost("drills")]
    public async Task<IActionResult> StartDrill([FromBody] CreateCvDrillDto dto, CancellationToken ct)
    {
        var result = await _personalSets.StartCvDrillAsync(User.GetUserId(), dto.Skill, ct);
        return SuccessResp.Ok(result);
    }

    [HttpGet("jobs/active")]
    public async Task<IActionResult> GetActiveJob()
    {
        var result = await _personalSets.GetLatestPendingCoachJobAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var result = await _personalSets.GetJobAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    [HttpGet("plan")]
    public async Task<IActionResult> GetPlan()
    {
        var result = await _skillPlans.GetAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }
}
