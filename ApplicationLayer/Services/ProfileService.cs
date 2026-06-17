using ApplicationLayer.DTOs.Profile;
using ApplicationLayer.Interfaces;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

/// <summary>SCRUM-161: Xem và cập nhật hồ sơ cá nhân.</summary>
public class ProfileService : IProfileService
{
    private readonly IUserRepository _userRepo;
    private readonly IHRProfileRepository _hrProfileRepo;
    private readonly ICandidateProfileRepository _candidateProfileRepo;
    private readonly ICompanyRepository _companyRepo;

    public ProfileService(
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

    public async Task<ProfileResponseDto> GetProfileAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        await _userRepo.LoadRoleAsync(user);
        return await MapToProfileResponseAsync(user);
    }

    public async Task<ProfileResponseDto> UpdateHRProfileAsync(Guid userId, UpdateHRProfileDto dto)
    {
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        await _userRepo.LoadRoleAsync(user);
        if (user.Role.Name != UserRole.HR)
            throw new ForbiddenException("Chỉ HR mới có thể cập nhật hồ sơ này.");

        // Resolve company: CompanyId hoặc CompanyName
        Guid? companyId = dto.CompanyId;
        if (companyId == null && !string.IsNullOrWhiteSpace(dto.CompanyName))
        {
            var existing = await _companyRepo.GetByNameAsync(dto.CompanyName.Trim());
            if (existing != null) companyId = existing.Id;
            else
            {
                var newCompany = new Company { Name = dto.CompanyName.Trim() };
                await _companyRepo.AddAsync(newCompany);
                companyId = newCompany.Id;
            }
        }

        // Update User fields
        user.FullName = dto.FullName;
        user.PhoneNumber = dto.PhoneNumber;
        user.AvatarUrl = dto.AvatarUrl;
        if (!user.IsProfileComplete) user.IsProfileComplete = true;

        // Upsert HRProfile
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
        return await MapToProfileResponseAsync(user);
    }

    public async Task<ProfileResponseDto> UpdateCandidateProfileAsync(Guid userId, UpdateCandidateProfileDto dto)
    {
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        await _userRepo.LoadRoleAsync(user);
        if (user.Role.Name != UserRole.Candidate)
            throw new ForbiddenException("Chỉ Candidate mới có thể cập nhật hồ sơ này.");

        user.FullName = dto.FullName;
        user.PhoneNumber = dto.PhoneNumber;
        user.AvatarUrl = dto.AvatarUrl;
        if (!user.IsProfileComplete) user.IsProfileComplete = true;

        var profile = await _candidateProfileRepo.GetByUserIdAsync(userId);
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
                Bio = dto.Bio
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
            await _candidateProfileRepo.UpdateAsync(profile);
        }

        await _userRepo.UpdateAsync(user);
        return await MapToProfileResponseAsync(user);
    }

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

    // ────────────────────────────────────────────────────────────────
    private async Task<ProfileResponseDto> MapToProfileResponseAsync(User user)
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
}
