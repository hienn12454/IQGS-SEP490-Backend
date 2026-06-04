using ApplicationLayer.DTOs.Company;
using ApplicationLayer.Interfaces;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

/// <summary>Quản lý danh sách công ty (cho HR chọn khi đăng ký).</summary>
[ApiController]
[Route("api/companies")]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompanyController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    /// <summary>Tìm kiếm công ty theo tên. Public — HR cần tra cứu khi đăng ký.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? keyword)
    {
        var result = await _companyService.SearchAsync(keyword);
        return SuccessResp.Ok(result);
    }

    /// <summary>Xem chi tiết một công ty.</summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _companyService.GetByIdAsync(id);
        return result == null
            ? ErrorResp.NotFound("Không tìm thấy công ty.")
            : SuccessResp.Ok(result);
    }

    /// <summary>Tạo công ty mới (HR/Admin).</summary>
    [Authorize(Roles = "HR,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyDto dto)
    {
        var result = await _companyService.CreateAsync(dto);
        return SuccessResp.Created(result);
    }
}
