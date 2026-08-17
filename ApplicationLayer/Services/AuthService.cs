using ApplicationLayer.DTOs.Auth;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace ApplicationLayer.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IHRProfileRepository _hrProfileRepo;
    private readonly ICandidateProfileRepository _candidateProfileRepo;
    private readonly ICompanyRepository _companyRepo;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly IGoogleTokenValidator _googleValidator;
    private readonly IGithubTokenValidator _githubValidator;
    private readonly IConfiguration _config;
    private readonly ISubscriptionService _subscriptionService;

    // ── Cấu hình ─────────────────────────────────────
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _lockoutDuration;
    private readonly int _refreshTokenExpirationDays;
    private readonly int _passwordResetTokenExpirationHours;
    private readonly int _emailVerificationOtpExpirationMinutes;

    public AuthService(
        IUserRepository userRepo,
        IHRProfileRepository hrProfileRepo,
        ICandidateProfileRepository candidateProfileRepo,
        ICompanyRepository companyRepo,
        IJwtService jwtService,
        IEmailService emailService,
        IGoogleTokenValidator googleValidator,
        IGithubTokenValidator githubValidator,
        IConfiguration config,
        ISubscriptionService subscriptionService)
    {
        _userRepo = userRepo;
        _hrProfileRepo = hrProfileRepo;
        _candidateProfileRepo = candidateProfileRepo;
        _companyRepo = companyRepo;
        _jwtService = jwtService;
        _emailService = emailService;
        _googleValidator = googleValidator;
        _githubValidator = githubValidator;
        _config = config;
        _subscriptionService = subscriptionService;

        _maxFailedAttempts = int.Parse(config["AuthSettings:MaxFailedAttempts"] ?? "5");
        _lockoutDuration = TimeSpan.FromMinutes(
            double.Parse(config["AuthSettings:LockoutDurationMinutes"] ?? "15"));
        _refreshTokenExpirationDays = int.Parse(
            config["JwtSettings:RefreshTokenExpirationDays"] ?? "7");
        _passwordResetTokenExpirationHours = int.Parse(
            config["AuthSettings:PasswordResetTokenExpirationHours"] ?? "1");
        _emailVerificationOtpExpirationMinutes = int.Parse(
            config["AuthSettings:EmailVerificationOtpExpirationMinutes"] ?? "10");
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-154 │ Login bằng email & password
    // ────────────────────────────────────────────────────────────────
    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _userRepo.GetByEmailAnyStatusAsync(request.Email);

        if (user == null)
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng. Vui lòng thử lại.");

        if (!user.IsActive)
            throw new UnauthorizedException("Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.");

        // Kiểm tra lockout
        if (user.LockoutUntil.HasValue && user.LockoutUntil > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
            throw new UnauthorizedException(
                $"Tài khoản tạm thời bị khóa do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau {remaining} phút.");
        }

        if (user.Provider != AuthProvider.Local && string.IsNullOrEmpty(user.PasswordHash))
        {
            var providerLabel = user.Provider == AuthProvider.Github ? "GitHub" : "Google";
            throw new BadRequestException(
                $"Tài khoản này đăng nhập qua {providerLabel}. Vui lòng sử dụng nút 'Đăng nhập với {providerLabel}'.");
        }

        if (!user.IsEmailVerified)
            throw new UnauthorizedException("Vui lòng xác minh email trước khi đăng nhập.");

        if (string.IsNullOrEmpty(user.PasswordHash)
            || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await HandleFailedLoginAsync(user);
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng. Vui lòng thử lại.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;

        return await IssueTokensAsync(user);
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-154 │ Google OAuth — Verify-only (bước 1)
    // ────────────────────────────────────────────────────────────────
    public async Task<OAuthVerifyResponseDto> VerifyGoogleTokenAsync(OAuthVerifyRequestDto request)
    {
        var account = await _googleValidator.ValidateAsync(request.IdToken);

        var user = await _userRepo.GetByGoogleIdAsync(account.Subject)
                   ?? await _userRepo.GetByEmailAsync(account.Email);

        var linkedToLocal = user != null && user.Provider == AuthProvider.Local;
        var isNewUser = user == null;

        return new OAuthVerifyResponseDto
        {
            IsNewUser = isNewUser,
            Email = account.Email,
            Name = account.Name ?? account.Email.Split('@')[0],
            Picture = account.Picture,
            EmailVerified = account.EmailVerified,
            LinkedToLocalAccount = linkedToLocal
        };
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-154 │ Google OAuth — Login / Register (bước 2)
    // ────────────────────────────────────────────────────────────────
    public async Task<LoginResponseDto> OAuthLoginAsync(OAuthLoginRequestDto request)
    {
        var account = await _googleValidator.ValidateAsync(request.IdToken);

        var user = await _userRepo.GetByGoogleIdAnyStatusAsync(account.Subject)
                   ?? await _userRepo.GetByEmailAnyStatusAsync(account.Email);
        var isNewUser = false;

        if (user != null && !user.IsActive)
            throw new UnauthorizedException("Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.");

        if (user == null)
        {
            user = await CreateOAuthUserAsync(account, request);
            isNewUser = true;
        }
        else if (user.GoogleId != account.Subject)
        {
            // SCRUM-405: liên kết Google với tài khoản local cùng email — giữ password.
            user.GoogleId = account.Subject;
            await _userRepo.UpdateAsync(user);
        }

        var response = await IssueTokensAsync(user);
        response.IsNewUser = isNewUser;
        return response;
    }

    // ────────────────────────────────────────────────────────────────
    // GitHub OAuth — Verify-only (bước 1). Học theo Google, khác ở chỗ
    // GitHub dùng Authorization Code flow (Code) thay vì ID Token.
    // ────────────────────────────────────────────────────────────────
    public async Task<OAuthVerifyResponseDto> VerifyGithubTokenAsync(OAuthGithubVerifyRequestDto request)
    {
        var account = await _githubValidator.ValidateAsync(request.Code);

        var user = await _userRepo.GetByGithubIdAsync(account.Id)
                   ?? await _userRepo.GetByEmailAsync(account.Email);

        var linkedToLocal = user != null && user.Provider == AuthProvider.Local;
        var isNewUser = user == null;

        return new OAuthVerifyResponseDto
        {
            IsNewUser = isNewUser,
            Email = account.Email,
            Name = account.Name ?? account.Login,
            Picture = account.AvatarUrl,
            EmailVerified = account.EmailVerified,
            LinkedToLocalAccount = linkedToLocal
        };
    }

    // ────────────────────────────────────────────────────────────────
    // GitHub OAuth — Login / Register (bước 2)
    // ────────────────────────────────────────────────────────────────
    public async Task<LoginResponseDto> GithubOAuthLoginAsync(OAuthGithubLoginRequestDto request)
    {
        var account = await _githubValidator.ValidateAsync(request.Code);

        var user = await _userRepo.GetByGithubIdAnyStatusAsync(account.Id)
                   ?? await _userRepo.GetByEmailAnyStatusAsync(account.Email);
        var isNewUser = false;

        if (user != null && !user.IsActive)
            throw new UnauthorizedException("Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.");

        if (user == null)
        {
            user = await CreateGithubOAuthUserAsync(account, request);
            isNewUser = true;
        }
        else if (user.GithubId != account.Id)
        {
            // Liên kết GitHub với tài khoản đã tồn tại cùng email — giữ password/GoogleId hiện có.
            user.GithubId = account.Id;
            await _userRepo.UpdateAsync(user);

            // Auto-fill field GithubUrl trong profile — chỉ khi đang trống, không ghi đè
            // link mà user đã tự nhập/chỉnh trước đó.
            await AutoFillGithubUrlAsync(user, account.ProfileUrl);
        }

        var response = await IssueTokensAsync(user);
        response.IsNewUser = isNewUser;
        return response;
    }

    // ────────────────────────────────────────────────────────────────
    // OAuth helpers — dùng chung cho Google & GitHub
    // ────────────────────────────────────────────────────────────────
    private Task<User> CreateOAuthUserAsync(GoogleAccountInfo account, OAuthLoginRequestDto request)
        => CreateOAuthUserCoreAsync(
            fullName: account.Name ?? account.Email.Split('@')[0],
            email: account.Email,
            emailVerified: account.EmailVerified,
            avatarUrl: account.Picture,
            provider: AuthProvider.Google,
            googleId: account.Subject,
            githubId: null,
            githubProfileUrl: null,
            intendedRole: request.IntendedRole,
            companyId: request.CompanyId,
            companyName: request.CompanyName,
            jobTitle: request.JobTitle,
            targetRole: request.TargetRole,
            seniorityLevel: request.SeniorityLevel,
            techStack: request.TechStack);

    private Task<User> CreateGithubOAuthUserAsync(GithubAccountInfo account, OAuthGithubLoginRequestDto request)
        => CreateOAuthUserCoreAsync(
            fullName: account.Name ?? account.Login,
            email: account.Email,
            emailVerified: account.EmailVerified,
            avatarUrl: account.AvatarUrl,
            provider: AuthProvider.Github,
            googleId: null,
            githubId: account.Id,
            githubProfileUrl: account.ProfileUrl,
            intendedRole: request.IntendedRole,
            companyId: request.CompanyId,
            companyName: request.CompanyName,
            jobTitle: request.JobTitle,
            targetRole: request.TargetRole,
            seniorityLevel: request.SeniorityLevel,
            techStack: request.TechStack);

    private async Task<User> CreateOAuthUserCoreAsync(
        string fullName,
        string email,
        bool emailVerified,
        string? avatarUrl,
        string provider,
        string? googleId,
        string? githubId,
        string? githubProfileUrl,
        string? intendedRole,
        Guid? companyId,
        string? companyName,
        string? jobTitle,
        string? targetRole,
        string? seniorityLevel,
        List<string>? techStack)
    {
        // Không cho phép tự đặt role Admin. Case-insensitive — báo lỗi nếu role lạ.
        var roleId = ParseIntendedRole(intendedRole);

        // Validate input TRƯỚC khi insert User (AddAsync = SaveChanges ngay) để tránh
        // tạo User mồ côi nếu profile-required field thiếu.
        if (roleId == UserRole.CandidateId)
        {
            if (string.IsNullOrWhiteSpace(targetRole))
                throw new BadRequestException("Vui lòng chọn vị trí mục tiêu mà bạn muốn ứng tuyển.");
            if (string.IsNullOrWhiteSpace(seniorityLevel))
                throw new BadRequestException("Vui lòng chọn cấp độ kinh nghiệm của bạn.");
        }

        // HR: resolve/create Company trước. Nếu thất bại, không có User mồ côi.
        Guid? hrCompanyId = null;
        if (roleId == UserRole.HRId)
            hrCompanyId = await ResolveOrCreateCompanyAsync(companyId, companyName);

        var user = new User
        {
            FullName = fullName,
            Email = email,
            RoleId = roleId,
            Provider = provider,
            GoogleId = googleId,
            GithubId = githubId,
            IsEmailVerified = emailVerified,
            IsProfileComplete = true,
            AvatarUrl = avatarUrl
        };
        await _userRepo.AddAsync(user);

        if (roleId == UserRole.HRId)
        {
            await _hrProfileRepo.AddAsync(new HRProfile
            {
                UserId = user.Id,
                CompanyId = hrCompanyId!.Value,
                JobTitle = jobTitle,
                // Auto-fill field GithubUrl khi đăng ký mới qua GitHub OAuth.
                GithubUrl = githubProfileUrl
            });
            // SCRUM-380: gán gói Free + LimitsSnapshot + kỳ anniversary
            await _subscriptionService.AssignFreePlanAsync(user.Id, SubscriptionAudience.HR);
        }
        else // Candidate
        {
            await _candidateProfileRepo.AddAsync(new CandidateProfile
            {
                UserId = user.Id,
                TargetRole = targetRole,
                SeniorityLevel = seniorityLevel,
                TechStack = (techStack ?? new List<string>()).ToArray(),
                // Auto-fill field GithubUrl khi đăng ký mới qua GitHub OAuth.
                GithubUrl = githubProfileUrl
            });
            await _subscriptionService.AssignFreePlanAsync(user.Id, SubscriptionAudience.Candidate);
        }

        return user;
    }

    /// <summary>
    /// Auto-fill field GithubUrl trong HRProfile/CandidateProfile khi user liên kết tài khoản
    /// GitHub vào một User đã tồn tại (returning user login qua GitHub lần đầu). Chỉ set khi
    /// đang trống — không ghi đè link mà user đã tự nhập/chỉnh trước đó.
    /// </summary>
    private async Task AutoFillGithubUrlAsync(User user, string githubProfileUrl)
    {
        if (user.RoleId == UserRole.HRId)
        {
            var profile = await _hrProfileRepo.GetByUserIdAsync(user.Id);
            if (profile != null && string.IsNullOrWhiteSpace(profile.GithubUrl))
            {
                profile.GithubUrl = githubProfileUrl;
                await _hrProfileRepo.UpdateAsync(profile);
            }
        }
        else if (user.RoleId == UserRole.CandidateId)
        {
            var profile = await _candidateProfileRepo.GetByUserIdAsync(user.Id);
            if (profile != null && string.IsNullOrWhiteSpace(profile.GithubUrl))
            {
                profile.GithubUrl = githubProfileUrl;
                await _candidateProfileRepo.UpdateAsync(profile);
            }
        }
    }

    private static int ParseIntendedRole(string? intendedRole)
    {
        if (string.IsNullOrWhiteSpace(intendedRole))
            return UserRole.CandidateId;

        if (string.Equals(intendedRole, UserRole.HR, StringComparison.OrdinalIgnoreCase))
            return UserRole.HRId;
        if (string.Equals(intendedRole, UserRole.Candidate, StringComparison.OrdinalIgnoreCase))
            return UserRole.CandidateId;

        throw new BadRequestException(
            "Vai trò không hợp lệ. Vui lòng chọn 'HR Manager' hoặc 'Ứng viên'.");
    }

    private async Task<Guid> ResolveOrCreateCompanyAsync(Guid? companyId, string? companyName)
    {
        if (companyId.HasValue)
        {
            var existing = await _companyRepo.GetByIdAsync(companyId.Value)
                ?? throw new BadRequestException("Không tìm thấy công ty bạn đã chọn. Vui lòng thử lại.");
            return existing.Id;
        }

        if (!string.IsNullOrWhiteSpace(companyName))
        {
            var existing = await _companyRepo.GetByNameAsync(companyName.Trim());
            if (existing != null) return existing.Id;

            var newCompany = new Company { Name = companyName.Trim() };
            await _companyRepo.AddAsync(newCompany);
            return newCompany.Id;
        }

        throw new BadRequestException("Vui lòng chọn công ty từ danh sách hoặc nhập tên công ty mới.");
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-147 │ Đăng ký HR Manager
    // ────────────────────────────────────────────────────────────────
    public async Task RegisterHRAsync(RegisterHRRequestDto request)
    {
        if (await _userRepo.GetByEmailAsync(request.Email) != null)
            throw new ConflictException("Email đã được sử dụng.");

        var companyId = await ResolveOrCreateCompanyAsync(request.CompanyId, request.CompanyName);

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            RoleId = UserRole.HRId,
            Provider = AuthProvider.Local,
            IsEmailVerified = false,
            IsProfileComplete = true
        };
        await _userRepo.AddAsync(user);

        await _hrProfileRepo.AddAsync(new HRProfile
        {
            UserId = user.Id,
            CompanyId = companyId,
            JobTitle = request.JobTitle
        });

        // SCRUM-380: gán gói HR Free
        await _subscriptionService.AssignFreePlanAsync(user.Id, SubscriptionAudience.HR);

        // SCRUM-148: gửi email xác minh
        await SendEmailVerificationAsync(user);
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-150 │ Đăng ký Candidate
    // ────────────────────────────────────────────────────────────────
    public async Task RegisterCandidateAsync(RegisterCandidateRequestDto request)
    {
        if (await _userRepo.GetByEmailAsync(request.Email) != null)
            throw new ConflictException("Email đã được sử dụng.");

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            RoleId = UserRole.CandidateId,
            Provider = AuthProvider.Local,
            IsEmailVerified = false,
            IsProfileComplete = true
        };
        await _userRepo.AddAsync(user);

        await _candidateProfileRepo.AddAsync(new CandidateProfile
        {
            UserId = user.Id,
            TargetRole = request.TargetRole,
            SeniorityLevel = request.SeniorityLevel,
            TechStack = request.TechStack.ToArray()
        });

        // SCRUM-380: gán gói Candidate Free
        await _subscriptionService.AssignFreePlanAsync(user.Id, SubscriptionAudience.Candidate);

        await SendEmailVerificationAsync(user);
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-151 │ Xác minh email
    // ────────────────────────────────────────────────────────────────
    public async Task VerifyEmailAsync(VerifyEmailDto request)
    {
        var user = await _userRepo.GetByEmailAsync(request.Email)
            ?? throw new BadRequestException("Email hoặc mã xác minh không đúng.");

        if (user.IsEmailVerified)
            throw new BadRequestException("Email đã được xác minh trước đó.");

        var otpHash = ComputeSha256(request.Otp.Trim());
        if (user.EmailVerificationToken != otpHash
            || user.EmailVerificationTokenExpiresAt == null
            || user.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
            throw new BadRequestException("Mã xác minh không đúng hoặc đã hết hạn.");

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresAt = null;
        await _userRepo.UpdateAsync(user);
    }

    public async Task ResendVerificationEmailAsync(ResendVerificationDto request)
    {
        var user = await _userRepo.GetByEmailAsync(request.Email);

        if (user == null || user.IsEmailVerified || user.Provider != AuthProvider.Local)
            return;

        await SendEmailVerificationAsync(user);
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-155 │ Refresh Token
    // ────────────────────────────────────────────────────────────────
    public async Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        var user = await _userRepo.GetByRefreshTokenAsync(request.RefreshToken)
            ?? throw new UnauthorizedException("Refresh token không hợp lệ.");

        if (user.RefreshTokenExpiresAt == null || user.RefreshTokenExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token đã hết hạn. Vui lòng đăng nhập lại.");

        return await IssueTokensAsync(user);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var user = await _userRepo.GetByRefreshTokenAsync(refreshToken);
        if (user == null) return;

        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _userRepo.UpdateAsync(user);
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-157 │ Quên mật khẩu
    // ────────────────────────────────────────────────────────────────
    public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request)
    {
        var user = await _userRepo.GetByEmailAsync(request.Email);

        if (user == null || user.Provider != AuthProvider.Local)
            return;

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = ComputeSha256(rawToken);

        user.PasswordResetToken = tokenHash;
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(_passwordResetTokenExpirationHours);
        await _userRepo.UpdateAsync(user);

        var frontendUrl = _config["AppSettings:FrontendUrl"] ?? "https://iqgs.com";
        var resetLink = $"{frontendUrl}/reset-password?token={rawToken}";

        await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, resetLink);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var tokenHash = ComputeSha256(request.Token.Trim());
        var user = await _userRepo.GetByPasswordResetTokenAsync(tokenHash);

        if (user == null)
            throw new BadRequestException(
                "Đường dẫn đặt lại không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu đường dẫn mới.");

        if (user.PasswordResetTokenExpiresAt == null
            || user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
            throw new BadRequestException(
                "Đường dẫn đặt lại không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu đường dẫn mới.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;

        await _userRepo.UpdateAsync(user);
    }

    // ────────────────────────────────────────────────────────────────
    // Private helpers
    // ────────────────────────────────────────────────────────────────
    private async Task SendEmailVerificationAsync(User user)
    {
        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otpHash = ComputeSha256(otp);

        user.EmailVerificationToken = otpHash;
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(_emailVerificationOtpExpirationMinutes);
        await _userRepo.UpdateAsync(user);

        await _emailService.SendEmailVerificationAsync(user.Email, user.FullName, otp);
    }

    private async Task<LoginResponseDto> IssueTokensAsync(User user)
    {
        // Đảm bảo Role được load để JWT có roleName
        if (user.Role == null!)
            await _userRepo.LoadRoleAsync(user);

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);
        await _userRepo.UpdateAsync(user);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = _jwtService.GetAccessTokenExpiry(),
            Role = user.Role?.Name ?? UserRole.GetNameById(user.RoleId),
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            IsProfileComplete = user.IsProfileComplete
        };
    }

    private async Task HandleFailedLoginAsync(User user)
    {
        user.FailedLoginAttempts++;

        if (user.FailedLoginAttempts >= _maxFailedAttempts)
        {
            user.LockoutUntil = DateTime.UtcNow.Add(_lockoutDuration);
            user.FailedLoginAttempts = 0;
        }

        await _userRepo.UpdateAsync(user);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
