using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.Profile;
using ApplicationLayer.Interfaces;
using DomainLayer.Constants;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

/// <summary>SCRUM-163: Quản lý người dùng cho Admin.</summary>
public class AdminService : IAdminService
{
    private readonly IUserRepository _userRepo;
    private readonly IHRProfileRepository _hrProfileRepo;
    private readonly ICandidateProfileRepository _candidateProfileRepo;
    private readonly ICompanyRepository _companyRepo;

    public AdminService(
        IUserRepository userRepo,
        IHRProfileRepository hrProfileRepo,
        ICandidateProfileRepository candidateProfileRepo,
        ICompanyRepository companyRepo)
    {
        _userRepo = userRepo;
        _hrProfileRepo = hrProfileRepo;
        _candidateProfileRepo = candidateProfileRepo;
        _companyRepo = companyRepo;
    }

    // AC-01/02: Danh sách phân trang + search + filter
    public async Task<PagedResultDto<UserListItemDto>> GetUsersAsync(UserQueryDto query)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, 100);

        var (users, total) = await _userRepo.GetPagedAsync(query);

        var items = users.Select(u => new UserListItemDto
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            Role = u.Role?.Name ?? UserRole.GetNameById(u.RoleId),
            IsActive = u.IsActive,
            IsEmailVerified = u.IsEmailVerified,
            IsProfileComplete = u.IsProfileComplete,
            Provider = u.Provider,
            CreatedAt = u.CreatedAt
        }).ToList();

        return new PagedResultDto<UserListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    // AC-03: Chi tiết user
    public async Task<UserDetailDto> GetUserDetailAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAnyStatusAsync(userId)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        var roleName = user.Role?.Name ?? UserRole.GetNameById(user.RoleId);

        var dto = new UserDetailDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = roleName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            IsActive = user.IsActive,
            IsEmailVerified = user.IsEmailVerified,
            IsProfileComplete = user.IsProfileComplete,
            Provider = user.Provider,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

        if (roleName == UserRole.HR)
        {
            var hr = await _hrProfileRepo.GetByUserIdAsync(user.Id);
            if (hr != null)
            {
                var company = await _companyRepo.GetByIdAsync(hr.CompanyId);
                dto.HRProfile = new HRProfileDto
                {
                    CompanyId = hr.CompanyId,
                    CompanyName = company?.Name ?? string.Empty,
                    JobTitle = hr.JobTitle,
                    PhoneNumber = hr.PhoneNumber,
                    LinkedInUrl = hr.LinkedInUrl,
                    Bio = hr.Bio,
                    IsCompanyVerified = hr.IsCompanyVerified
                };
            }
        }
        else if (roleName == UserRole.Candidate)
        {
            var c = await _candidateProfileRepo.GetByUserIdAsync(user.Id);
            if (c != null)
                dto.CandidateProfile = new CandidateProfileDto
                {
                    TargetRole = c.TargetRole,
                    SeniorityLevel = c.SeniorityLevel,
                    TechStack = c.TechStack.ToList(),
                    PhoneNumber = c.PhoneNumber,
                    LinkedInUrl = c.LinkedInUrl,
                    GithubUrl = c.GithubUrl,
                    Bio = c.Bio
                };
        }

        return dto;
    }

    // AC-04: Enable / Disable
    public async Task UpdateUserStatusAsync(Guid userId, bool isActive)
    {
        var user = await _userRepo.GetByIdAnyStatusAsync(userId)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        user.IsActive = isActive;

        if (!isActive)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = null;
        }

        await _userRepo.UpdateAsync(user);
    }
}
