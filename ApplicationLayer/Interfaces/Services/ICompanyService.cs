using ApplicationLayer.DTOs.Company;

namespace ApplicationLayer.Interfaces.Services;

public interface ICompanyService
{
    Task<List<CompanyDto>> SearchAsync(string? keyword);
    Task<CompanyDto?> GetByIdAsync(Guid id);
    Task<CompanyDto> CreateAsync(CreateCompanyDto dto);
}
