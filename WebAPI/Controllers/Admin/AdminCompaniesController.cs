using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

/// <summary>SCRUM-480: Admin quản lý công ty — phân trang (khác GET /api/companies public).</summary>
[ApiController]
[Route("api/admin/companies")]
[Authorize(Roles = "Admin")]
public class AdminCompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public AdminCompaniesController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    /// <summary>Danh sách công ty phân trang + keyword + totalCount.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _companyService.SearchPagedAsync(keyword, page, pageSize);
        return SuccessResp.Ok(result);
    }
}
