using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.ResponseCode;
using ApplicationLayer.Services.Coach;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

/// <summary>SCRUM-457: Role Family + alias là config — thêm domain SE không sửa resolver.</summary>
[ApiController]
[Route("api/admin/competency-role-families")]
[Authorize(Roles = "Admin")]
public class AdminCompetencyRoleFamilyController : ControllerBase
{
    private readonly ICompetencyRoleFamilyAdminService _families;

    public AdminCompetencyRoleFamilyController(ICompetencyRoleFamilyAdminService families)
        => _families = families;

    [HttpGet]
    public async Task<IActionResult> List()
        => SuccessResp.Ok(await _families.ListAsync());

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertCompetencyRoleFamilyDto dto)
        => SuccessResp.Ok(await _families.UpsertAsync(dto));
}
