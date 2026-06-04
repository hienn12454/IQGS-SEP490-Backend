using ApplicationLayer.DTOs.Auth;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Google.Apis.Auth;
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
    private readonly IConfiguration _config;

    // ── Cấu hình ─────────────────────────────────────
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _lockoutDuration;
    private readonly int _refreshTokenExpirationDays;
    private readonly int _passwordResetTokenExpirationHours;
    private readonly int _emailVerificationTokenExpirationHours;

    public AuthService(
        IUserRepository userRepo,
        IHRProfileRepository hrProfileRepo,
        ICandidateProfileRepository candidateProfileRepo,
        ICompanyRepository companyRepo,
        IJwtService jwtService,
        IEmailService emailService,
        IConfiguration config)
    {
        _userRepo = userRepo;
        _hrProfileRepo = hrProfileRepo;
        _candidateProfileRepo = candidateProfileRepo;
        _companyRepo = companyRepo;
        _jwtService = jwtService;
        _emailService = emailService;
        _config = config;

        _maxFailedAttempts = int.Parse(config["AuthSettings:MaxFailedAttempts"] ?? "5");
        _lockoutDuration = TimeSpan.FromMinutes(
            double.Parse(config["AuthSettings:LockoutDurationMinutes"] ?? "15"));
        _refreshTokenExpirationDays = int.Parse(
            config["JwtSettings:RefreshTokenExpirationDays"] ?? "7");
        _passwordResetTokenExpirationHours = int.Parse(
            config["AuthSettings:PasswordResetTokenExpirationHours"] ?? "1");
        _emailVerificationTokenExpirationHours = int.Parse(
            config["AuthSettings:EmailVerificationTokenExpirationHours"] ?? "24");
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-154 │ Login bằng email & password
    // ────────────────────────────────────────────────────────────────
    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _userRepo.GetByEmailAsync(request.Email);

        if (user == null)
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng. Vui lòng thử lại.");

        // Kiểm tra lockout
        if (user.LockoutUntil.HasValue && user.LockoutUntil > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
            throw new UnauthorizedException(
                $"Tài khoản tạm thời bị khóa do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau {remaining} phút.");
        }

        if (user.Provider != AuthProvider.Local)
            throw new BadRequestException(
                "Tài khoản này đăng nhập qua Google. Vui lòng sử dụng nút 'Đăng nhập với Google'.");

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
    // SCRUM-154 │ Google OAuth
    // ────────────────────────────────────────────────────────────────
    public async Task<LoginResponseDto> OAuthLoginAsync(OAuthLoginRequestDto request)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var googleClientId = _config["GoogleOAuth:ClientId"]
                ?? throw new InvalidOperationException("GoogleOAuth:ClientId chưa được cấu hình.");

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [googleClientId]
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedException("Google ID Token không hợp lệ hoặc đã hết hạn.");
        }

        var user = await _userRepo.GetByGoogleIdAsync(payload.Subject);

        if (user == null)
        {
            user = await _userRepo.GetByEmailAsync(payload.Email);

            if (user != null)
            {
                if (user.Provider != AuthProvider.Google)
                    throw new ConflictException(
                        "Email này đã được đăng ký bằng mật khẩu. Vui lòng đăng nhập bằng email và mật khẩu.");

                user.GoogleId = payload.Subject;
                await _userRepo.UpdateAsync(user);
            }
            else
            {
                // Tạo tài khoản mới từ Google (AC-00 SCRUM-147/150)
                // Không cho phép tự đặt role Admin
                var intendedRoleId = request.IntendedRole switch
                {
                    UserRole.HR => UserRole.HRId,
                    UserRole.Candidate => UserRole.CandidateId,
                    _ => UserRole.CandidateId
                };

                user = new User
                {
                    FullName = payload.Name ?? payload.Email.Split('@')[0],
                    Email = payload.Email,
                    RoleId = intendedRoleId,
                    Provider = AuthProvider.Google,
                    GoogleId = payload.Subject,
                    IsEmailVerified = payload.EmailVerified,
                    IsProfileComplete = false,    // phải hoàn thiện hồ sơ
                    AvatarUrl = payload.Picture
                };
                await _userRepo.AddAsync(user);
            }
        }

        return await IssueTokensAsync(user);
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-147 │ Đăng ký HR Manager
    // ────────────────────────────────────────────────────────────────
    public async Task RegisterHRAsync(RegisterHRRequestDto request)
    {
        if (await _userRepo.GetByEmailAsync(request.Email) != null)
            throw new ConflictException("Email đã được sử dụng.");

        // Resolve company: dùng CompanyId nếu có, hoặc tạo Company mới từ CompanyName
        Guid companyId;
        if (request.CompanyId.HasValue)
        {
            var existing = await _companyRepo.GetByIdAsync(request.CompanyId.Value)
                ?? throw new BadRequestException("Công ty không tồn tại.");
            companyId = existing.Id;
        }
        else if (!string.IsNullOrWhiteSpace(request.CompanyName))
        {
            var existing = await _companyRepo.GetByNameAsync(request.CompanyName.Trim());
            if (existing != null)
            {
                companyId = existing.Id;
            }
            else
            {
                var newCompany = new Company { Name = request.CompanyName.Trim() };
                await _companyRepo.AddAsync(newCompany);
                companyId = newCompany.Id;
            }
        }
        else
        {
            throw new BadRequestException("Phải cung cấp CompanyId hoặc CompanyName.");
        }

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

        // SCRUM-148: gửi email xác minh
        await SendEmailVerificationAsync(user);
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-150 │ Đăng ký Candidate
    // ────────────────────────────────────────────────────────────────
    public async Task<LoginResponseDto> RegisterCandidateAsync(RegisterCandidateRequestDto request)
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
            IsEmailVerified = true,
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

        return await IssueTokensAsync(user);
    }

    // ────────────────────────────────────────────────────────────────
    // SCRUM-151 │ Xác minh email
    // ────────────────────────────────────────────────────────────────
    public async Task VerifyEmailAsync(VerifyEmailDto request)
    {
        var tokenHash = ComputeSha256(request.Token.Trim());
        var user = await _userRepo.GetByEmailVerificationTokenAsync(tokenHash);

        if (user == null)
            throw new BadRequestException(
                "Đường dẫn xác minh đã hết hạn. Vui lòng yêu cầu đường dẫn mới.");

        if (user.EmailVerificationTokenExpiresAt == null
            || user.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
            throw new BadRequestException(
                "Đường dẫn xác minh đã hết hạn. Vui lòng yêu cầu đường dẫn mới.");

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
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = ComputeSha256(rawToken);

        user.EmailVerificationToken = tokenHash;
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(_emailVerificationTokenExpirationHours);
        await _userRepo.UpdateAsync(user);

        var frontendUrl = _config["AppSettings:FrontendUrl"] ?? "https://iqgs.com";
        var verifyLink = $"{frontendUrl}/verify-email?token={rawToken}";
        await _emailService.SendEmailVerificationAsync(user.Email, user.FullName, verifyLink);
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
