using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.Company;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

/// <summary>SCRUM-480: Admin liệt kê công ty có phân trang (public /api/companies giữ limit 50 cho HR register).</summary>
[ApiController]
[Route("api/admin/companies")]
[Authorize(Roles = "Admin")]
public class AdminCompanyController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public AdminCompanyController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    /// <summary>Danh sách công ty active — page, pageSize, keyword → { items, totalCount, page, pageSize }.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminCompanyListQueryDto query)
    {
        var result = await _companyService.SearchPagedAsync(query);
        return SuccessResp.Ok(result);
    }
}
