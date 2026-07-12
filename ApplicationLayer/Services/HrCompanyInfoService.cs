using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;

namespace ApplicationLayer.Services;

public class HrCompanyInfoService : IHrCompanyInfoService
{
    private readonly IHRProfileRepository _hrProfileRepository;
    private readonly ICompanyRepository _companyRepository;

    public HrCompanyInfoService(IHRProfileRepository hrProfileRepository, ICompanyRepository companyRepository)
    {
        _hrProfileRepository = hrProfileRepository;
        _companyRepository = companyRepository;
    }

    public async Task<(string Name, string Logo)> GetByHrUserIdAsync(Guid hrUserId)
    {
        var hrProfile = await _hrProfileRepository.GetByUserIdAsync(hrUserId);
        if (hrProfile is null)
            return (string.Empty, CompanyLogoResolver.Resolve(null, null, string.Empty));

        var company = await _companyRepository.GetByIdAsync(hrProfile.CompanyId);
        var name = company?.Name ?? string.Empty;
        return (name, CompanyLogoResolver.Resolve(company?.LogoUrl, company?.WebsiteUrl, name));
    }
}
