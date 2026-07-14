using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.Profile;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IHRProfileRepository _hrProfileRepo;
    private readonly ICandidateProfileRepository _candidateProfileRepo;
    private readonly ICompanyRepository _companyRepo;

    public UserService(
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

    // ── Admin: danh sách phân trang ───────────────────────────────────

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

    // ── Admin: chi tiết user (bất kể IsActive) ───────────────────────

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

        await FillRoleSpecificProfileAsync(dto, user.Id, roleName);
        return dto;
    }

    // ── Admin: enable / disable ───────────────────────────────────────

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

    // ── Me: xem profile ───────────────────────────────────────────────

    public async Task<ProfileResponseDto> GetProfileAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        await _userRepo.LoadRoleAsync(user);
        return await BuildProfileResponseAsync(user);
    }

    // ── Me: cập nhật HR profile ───────────────────────────────────────

    public async Task<ProfileResponseDto> UpdateHRProfileAsync(Guid userId, UpdateHRProfileDto dto)
    {
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        await _userRepo.LoadRoleAsync(user);
        if (user.Role.Name != UserRole.HR)
            throw new ForbiddenException("Chỉ HR mới có thể cập nhật hồ sơ này.");

        var companyId = await ResolveCompanyIdAsync(dto.CompanyId, dto.CompanyName);

        user.FullName = dto.FullName;
        user.PhoneNumber = dto.PhoneNumber;
        user.AvatarUrl = dto.AvatarUrl;
        if (!user.IsProfileComplete) user.IsProfileComplete = true;

        var profile = await _hrProfileRepo.GetByUserIdAsync(userId);
        if (profile == null)
        {
            if (companyId == null)
                throw new BadRequestException("Phải cung cấp CompanyId hoặc CompanyName.");

            await _hrProfileRepo.AddAsync(new HRProfile
            {
                UserId = userId,
                CompanyId = companyId.Value,
                JobTitle = dto.JobTitle,
                PhoneNumber = dto.PhoneNumber,
                LinkedInUrl = dto.LinkedInUrl,
                Bio = dto.Bio
            });
        }
        else
        {
            if (companyId.HasValue) profile.CompanyId = companyId.Value;
            profile.JobTitle = dto.JobTitle;
            profile.PhoneNumber = dto.PhoneNumber;
            profile.LinkedInUrl = dto.LinkedInUrl;
            profile.Bio = dto.Bio;
            await _hrProfileRepo.UpdateAsync(profile);
        }

        await _userRepo.UpdateAsync(user);
        return await BuildProfileResponseAsync(user);
    }

    // ── Me: cập nhật Candidate profile ───────────────────────────────

    public async Task<ProfileResponseDto> UpdateCandidateProfileAsync(Guid userId, UpdateCandidateProfileDto dto)
    {
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        await _userRepo.LoadRoleAsync(user);
        if (user.Role.Name != UserRole.Candidate)
            throw new ForbiddenException("Chỉ Candidate mới có thể cập nhật hồ sơ này.");

        var profile = await _candidateProfileRepo.GetByUserIdAsync(userId);

        // Candidate tự tay đổi field nào (khác giá trị đang lưu) thì khóa vĩnh viễn field đó khỏi CV sync —
        // gửi lại đúng giá trị cũ (FE luôn resend cả form) thì không tính là "tự sửa", không bị khóa oan.
        var newlyChangedFields = new List<string>();
        if (dto.FullName != user.FullName)
            newlyChangedFields.Add(CvSyncableProfileFields.FullName);
        if (dto.PhoneNumber != (profile?.PhoneNumber ?? user.PhoneNumber))
            newlyChangedFields.Add(CvSyncableProfileFields.PhoneNumber);
        if (dto.Address != profile?.Address)
            newlyChangedFields.Add(CvSyncableProfileFields.Address);
        if (dto.GithubUrl != profile?.GithubUrl)
            newlyChangedFields.Add(CvSyncableProfileFields.GithubUrl);
        if (dto.LinkedInUrl != profile?.LinkedInUrl)
            newlyChangedFields.Add(CvSyncableProfileFields.LinkedInUrl);

        var lockedFields = (profile?.CvSyncLockedFields ?? Array.Empty<string>())
            .Union(newlyChangedFields)
            .ToArray();

        user.FullName = dto.FullName;
        user.PhoneNumber = dto.PhoneNumber;
        user.AvatarUrl = dto.AvatarUrl;
        if (!user.IsProfileComplete) user.IsProfileComplete = true;

        if (profile == null)
        {
            await _candidateProfileRepo.AddAsync(new CandidateProfile
            {
                UserId = userId,
                TargetRole = dto.TargetRole,
                SeniorityLevel = dto.SeniorityLevel,
                TechStack = dto.TechStack.ToArray(),
                PhoneNumber = dto.PhoneNumber,
                LinkedInUrl = dto.LinkedInUrl,
                GithubUrl = dto.GithubUrl,
                Bio = dto.Bio,
                Address = dto.Address,
                CvSyncLockedFields = lockedFields
            });
        }
        else
        {
            profile.TargetRole = dto.TargetRole;
            profile.SeniorityLevel = dto.SeniorityLevel;
            profile.TechStack = dto.TechStack.ToArray();
            profile.PhoneNumber = dto.PhoneNumber;
            profile.LinkedInUrl = dto.LinkedInUrl;
            profile.GithubUrl = dto.GithubUrl;
            profile.Bio = dto.Bio;
            profile.Address = dto.Address;
            profile.CvSyncLockedFields = lockedFields;
            await _candidateProfileRepo.UpdateAsync(profile);
        }

        await _userRepo.UpdateAsync(user);
        return await BuildProfileResponseAsync(user);
    }

    // ── Me: đổi mật khẩu ─────────────────────────────────────────────

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        if (user.Provider != AuthProvider.Local || string.IsNullOrEmpty(user.PasswordHash))
            throw new BadRequestException("Tài khoản OAuth không hỗ trợ đổi mật khẩu bằng cách này.");

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            throw new BadRequestException("Mật khẩu hiện tại không đúng.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;

        await _userRepo.UpdateAsync(user);
    }

    // ── Private helpers ───────────────────────────────────────────────

    private async Task<ProfileResponseDto> BuildProfileResponseAsync(User user)
    {
        var roleName = user.Role?.Name ?? UserRole.GetNameById(user.RoleId);

        var dto = new ProfileResponseDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = roleName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            IsEmailVerified = user.IsEmailVerified,
            IsProfileComplete = user.IsProfileComplete,
            Provider = user.Provider,
            CreatedAt = user.CreatedAt
        };

        await FillRoleSpecificProfileAsync(dto, user.Id, roleName);
        return dto;
    }

    /// <summary>Điền HRProfile hoặc CandidateProfile vào dto (dùng chung cho cả admin detail và me profile).</summary>
    private async Task FillRoleSpecificProfileAsync(dynamic dto, Guid userId, string roleName)
    {
        if (roleName == UserRole.HR)
        {
            var hr = await _hrProfileRepo.GetByUserIdAsync(userId);
            if (hr != null)
            {
                var company = await _companyRepo.GetByIdAsync(hr.CompanyId);
                dto.HRProfile = new HRProfileDto
                {
                    CompanyId = hr.CompanyId,
                    CompanyName = company?.Name ?? string.Empty,
                    CompanyLogo = CompanyLogoResolver.Resolve(
                        company?.LogoUrl, company?.WebsiteUrl, company?.Name ?? string.Empty),
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
            var c = await _candidateProfileRepo.GetByUserIdAsync(userId);
            if (c != null)
                dto.CandidateProfile = new CandidateProfileDto
                {
                    TargetRole = c.TargetRole,
                    SeniorityLevel = c.SeniorityLevel,
                    TechStack = c.TechStack.ToList(),
                    PhoneNumber = c.PhoneNumber,
                    LinkedInUrl = c.LinkedInUrl,
                    GithubUrl = c.GithubUrl,
                    Bio = c.Bio,
                    Address = c.Address,
                    CvFileName = c.CvFileName,
                    CvUploadedAt = c.CvUploadedAt,
                    AutoSyncProfileFromCv = c.AutoSyncProfileFromCv
                };
        }
    }

    private async Task<Guid?> ResolveCompanyIdAsync(Guid? companyId, string? companyName)
    {
        if (companyId.HasValue) return companyId;
        if (string.IsNullOrWhiteSpace(companyName)) return null;

        var existing = await _companyRepo.GetByNameAsync(companyName.Trim());
        if (existing != null) return existing.Id;

        var newCompany = new Company { Name = companyName.Trim() };
        await _companyRepo.AddAsync(newCompany);
        return newCompany.Id;
    }
}
