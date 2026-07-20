using ApplicationLayer.DTOs.Company;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _companyRepo;
    private readonly IHRProfileRepository _hrProfileRepo;

    public CompanyService(ICompanyRepository companyRepo, IHRProfileRepository hrProfileRepo)
    {
        _companyRepo = companyRepo;
        _hrProfileRepo = hrProfileRepo;
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

    public async Task<List<CompanyDto>> CreateManyAsync(BulkCreateCompaniesDto dto)
    {
        var duplicateNames = dto.Companies
            .GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateNames.Count > 0)
            throw new BadRequestException(
                $"Tên công ty bị trùng trong danh sách: {string.Join(", ", duplicateNames)}.");

        var companies = dto.Companies.Select(d => new Company
        {
            Name = d.Name.Trim(),
            LogoUrl = d.LogoUrl,
            WebsiteUrl = d.WebsiteUrl,
            Description = d.Description
        }).ToList();

        // All-or-nothing: 1 SaveChanges duy nhất — lỗi giữa chừng thì không công ty nào được tạo.
        await _companyRepo.AddRangeAsync(companies);

        return companies.Select(MapToDto).ToList();
    }

    public async Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyDto dto, Guid currentUserId, bool isAdmin)
    {
        var company = await EnsureCanManageAsync(id, currentUserId, isAdmin);

        company.Name = dto.Name;
        company.LogoUrl = dto.LogoUrl;
        company.WebsiteUrl = dto.WebsiteUrl;
        company.Description = dto.Description;
        await _companyRepo.UpdateAsync(company);

        return MapToDto(company);
    }

    public async Task DeleteAsync(Guid id, Guid currentUserId, bool isAdmin)
    {
        var company = await EnsureCanManageAsync(id, currentUserId, isAdmin);
        await _companyRepo.DeleteAsync(company);
    }

    private async Task<Company> EnsureCanManageAsync(Guid companyId, Guid currentUserId, bool isAdmin)
    {
        var company = await _companyRepo.GetByIdAsync(companyId)
            ?? throw new NotFoundException("Không tìm thấy công ty.");

        if (!isAdmin)
        {
            var hrProfile = await _hrProfileRepo.GetByUserIdAsync(currentUserId);
            if (hrProfile == null || hrProfile.CompanyId != companyId)
                throw new ForbiddenException("Bạn không có quyền quản lý công ty này.");
        }

        return company;
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
