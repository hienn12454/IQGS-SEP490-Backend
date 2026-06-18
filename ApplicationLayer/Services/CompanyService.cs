using ApplicationLayer.DTOs.Company;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;

namespace ApplicationLayer.Services;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _companyRepo;

    public CompanyService(ICompanyRepository companyRepo)
    {
        _companyRepo = companyRepo;
    }

    public async Task<List<CompanyDto>> SearchAsync(string? keyword)
    {
        var companies = await _companyRepo.SearchAsync(keyword);
        return companies.Select(MapToDto).ToList();
    }

    public async Task<CompanyDto?> GetByIdAsync(Guid id)
    {
        var company = await _companyRepo.GetByIdAsync(id);
        return company == null ? null : MapToDto(company);
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyDto dto)
    {
        var company = new Company
        {
            Name = dto.Name,
            LogoUrl = dto.LogoUrl,
            WebsiteUrl = dto.WebsiteUrl,
            Description = dto.Description
        };
        await _companyRepo.AddAsync(company);
        return MapToDto(company);
    }

    private static CompanyDto MapToDto(Company c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        LogoUrl = c.LogoUrl,
        WebsiteUrl = c.WebsiteUrl,
        Description = c.Description,
        CreatedAt = c.CreatedAt
    };
}
