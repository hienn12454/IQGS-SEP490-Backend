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
    private readonly ICoachCompetencyService _coach;
    private readonly ICandidatePracticeSessionService _practiceSessions;

    public CandidateCoachController(
        ICandidatePersonalSetService personalSets,
        ICandidateSkillPlanService skillPlans,
        ICoachCompetencyService coach,
        ICandidatePracticeSessionService practiceSessions)
    {
        _personalSets = personalSets;
        _skillPlans = skillPlans;
        _coach = coach;
        _practiceSessions = practiceSessions;
    }

    /// <summary>SCRUM-447: lấy context Coach (CV analysis + confirmed goals).</summary>
    [HttpGet("context")]
    public async Task<IActionResult> GetContext()
    {
        var result = await _coach.GetContextAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-453: catalog Target Role đang có framework (data-driven, không hardcode stack).</summary>
    [HttpGet("frameworks")]
    public async Task<IActionResult> ListFrameworks()
    {
        var result = await _coach.ListFrameworkCatalogAsync();
        return SuccessResp.Ok(result);
    }

    [HttpPut("context")]
    public async Task<IActionResult> UpdateContext([FromBody] UpdateCoachContextDto dto)
    {
        var result = await _coach.UpdateContextAsync(User.GetUserId(), dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>SCRUM-459: soft-reset vòng Coach — về Confirm Goal, giữ CV.</summary>
    [HttpPost("reset-run")]
    public async Task<IActionResult> ResetRun()
    {
        var result = await _coach.ResetCoachRunAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Diagnostic competency — Hangfire async (SCRUM-447).</summary>
    [HttpPost("diagnostic")]
    public async Task<IActionResult> StartDiagnostic(CancellationToken ct)
    {
        var result = await _coach.StartDiagnosticAsync(User.GetUserId(), ct);
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

    /// <summary>Huỷ job đang Queued/Generating — cho phép retry Coach.</summary>
    [HttpPost("jobs/{id:guid}/cancel")]
    public async Task<IActionResult> CancelJob(Guid id)
    {
        var result = await _coach.CancelActiveJobAsync(User.GetUserId(), id);
        return SuccessResp.Ok(result);
    }

    [HttpGet("plan")]
    public async Task<IActionResult> GetPlan()
    {
        var result = await _skillPlans.GetAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    [HttpGet("report")]
    public async Task<IActionResult> GetReport()
    {
        var result = await _coach.GetLatestReportAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Chấm lại diagnostic từ session đã nộp — mở khóa Báo cáo/Lộ trình nếu Complete nuốt scoring.</summary>
    [HttpPost("report/rescore")]
    public async Task<IActionResult> RescoreReport()
    {
        await _practiceSessions.RescoreLatestCoachDiagnosticAsync(User.GetUserId());
        var result = await _coach.GetLatestReportAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var result = await _coach.GetHistoryAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    [HttpGet("assessments/{id:guid}")]
    public async Task<IActionResult> GetAssessment(Guid id)
    {
        var result = await _coach.GetAssessmentAsync(User.GetUserId(), id);
        return SuccessResp.Ok(result);
    }

    [HttpGet("roadmaps")]
    public async Task<IActionResult> ListRoadmaps()
    {
        var result = await _coach.ListRoadmapsAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    [HttpPost("roadmaps/{id:guid}/start")]
    public async Task<IActionResult> StartRoadmap(Guid id)
    {
        var result = await _coach.StartRoadmapAsync(User.GetUserId(), id);
        return SuccessResp.Ok(result);
    }

    [HttpGet("roadmaps/{id:guid}")]
    public async Task<IActionResult> GetRoadmap(Guid id)
    {
        var result = await _coach.GetRoadmapAsync(User.GetUserId(), id);
        return SuccessResp.Ok(result);
    }

    [HttpPost("roadmaps/{roadmapId:guid}/items/{itemId:guid}/drill")]
    public async Task<IActionResult> StartItemDrill(Guid roadmapId, Guid itemId, CancellationToken ct)
    {
        var result = await _coach.StartDrillForRoadmapItemAsync(User.GetUserId(), roadmapId, itemId, ct);
        return SuccessResp.Ok(result);
    }

    [HttpPost("roadmaps/{id:guid}/reassessment")]
    public async Task<IActionResult> StartReassessment(Guid id, CancellationToken ct)
    {
        var result = await _coach.StartReassessmentAsync(User.GetUserId(), id, ct);
        return SuccessResp.Ok(result);
    }
}
