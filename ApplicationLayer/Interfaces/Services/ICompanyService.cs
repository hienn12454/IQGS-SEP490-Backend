using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.Company;

namespace ApplicationLayer.Interfaces.Services;

public interface ICompanyService
{
    Task<List<CompanyDto>> SearchAsync(string? keyword);
    /// <summary>SCRUM-480: danh sách công ty phân trang cho Admin.</summary>
    Task<PagedResultDto<CompanyDto>> SearchPagedAsync(AdminCompanyListQueryDto query);
    Task<CompanyDto?> GetByIdAsync(Guid id);
    Task<CompanyDto> CreateAsync(CreateCompanyDto dto);
    Task<List<CompanyDto>> CreateManyAsync(BulkCreateCompaniesDto dto);
    Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyDto dto, Guid currentUserId, bool isAdmin);
    Task DeleteAsync(Guid id, Guid currentUserId, bool isAdmin);
}
