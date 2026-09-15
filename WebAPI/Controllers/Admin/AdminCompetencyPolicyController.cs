using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.ResponseCode;
using ApplicationLayer.Services.Coach;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

/// <summary>
/// SCRUM-453/454: tinh chỉnh scoring policy + level rules toàn cục.
/// Không gắn role/technology — .NET/Java/Python/React dùng chung một bộ ngưỡng.
/// </summary>
[ApiController]
[Route("api/admin/competency-policies")]
[Authorize(Roles = "Admin")]
public class AdminCompetencyPolicyController : ControllerBase
{
    private readonly ICompetencyPolicyAdminService _policies;

    public AdminCompetencyPolicyController(ICompetencyPolicyAdminService policies)
        => _policies = policies;

    [HttpGet("scoring")]
    public async Task<IActionResult> GetScoring()
        => SuccessResp.Ok(await _policies.GetScoringPolicyAsync());

    [HttpPut("scoring")]
    public async Task<IActionResult> UpdateScoring([FromBody] CompetencyScoringPolicyDto dto)
        => SuccessResp.Ok(await _policies.UpdateScoringPolicyAsync(dto));

    [HttpGet("level-rules")]
    public async Task<IActionResult> ListLevelRules()
        => SuccessResp.Ok(await _policies.ListLevelRulesAsync());

    [HttpPut("level-rules")]
    public async Task<IActionResult> UpdateLevelRules([FromBody] UpdateCompetencyLevelRulesDto dto)
        => SuccessResp.Ok(await _policies.UpdateLevelRulesAsync(dto.Rules));
}
