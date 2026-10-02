using ApplicationLayer.Studio.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Studio;

/// <summary>Catalog TechSkill cho dropdown focus Studio — FE không hardcode lệch enum.</summary>
[ApiController]
[Authorize(Roles = "HR")]
[Route("api/studio/tech-skills")]
public sealed class StudioTechSkillsController : ControllerBase
{
    [HttpGet]
    public IActionResult List() => Ok(TechSkillCatalog.All);
}
