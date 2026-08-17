using ApplicationLayer.DTOs.Auth;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

/// <summary>
/// SCRUM-154: Đăng nhập + cấp JWT (email/password &amp; Google OAuth)
/// SCRUM-155: Refresh token + giới hạn đăng nhập thất bại + logout
/// SCRUM-157: Quên mật khẩu + đặt lại mật khẩu qua email
/// GitHub OAuth: học theo flow Google — /oauth/github/verify + /oauth/github.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/login
    // AC-01..06 từ SCRUM-154
    // ────────────────────────────────────────────────────────────────
    /// <summary>Đăng nhập bằng email và mật khẩu, nhận JWT + refresh token.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);
        return SuccessResp.Ok(result);
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/oauth/google/verify
    // Bước 1 OAuth flow: chỉ verify Google ID Token, KHÔNG tạo user, KHÔNG cấp JWT.
    // FE dùng IsNewUser trong response để rẽ nhánh: returning → gọi /oauth/google luôn;
    // new → hiện màn chọn role + hoàn tất hồ sơ, rồi mới gọi /oauth/google với đủ thông tin.
    // ────────────────────────────────────────────────────────────────
    /// <summary>Verify Google ID Token để biết user mới hay cũ trước khi tạo account.</summary>
    [AllowAnonymous]
    [HttpPost("oauth/google/verify")]
    public async Task<IActionResult> GoogleVerify([FromBody] OAuthVerifyRequestDto request)
    {
        var result = await _authService.VerifyGoogleTokenAsync(request);
        return SuccessResp.Ok(result);
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/oauth/google
    // Bước 2 OAuth flow: đăng nhập (returning user) hoặc tạo user + profile (new user).
    // AC-06 SCRUM-154 + AC-00 SCRUM-147/150.
    // ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Đăng nhập / đăng ký bằng Google OAuth.
    /// Với user mới phải kèm IntendedRole + các field profile (CompanyId/CompanyName/JobTitle cho HR;
    /// TargetRole/SeniorityLevel/TechStack cho Candidate).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("oauth/google")]
    public async Task<IActionResult> GoogleLogin([FromBody] OAuthLoginRequestDto request)
    {
        var result = await _authService.OAuthLoginAsync(request);
        return SuccessResp.Ok(result);
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/oauth/github/verify
    // Bước 1 GitHub OAuth flow: đổi Authorization Code lấy thông tin tài khoản, KHÔNG tạo user,
    // KHÔNG cấp JWT. Học theo /oauth/google/verify.
    // ────────────────────────────────────────────────────────────────
    /// <summary>Verify GitHub Authorization Code để biết user mới hay cũ trước khi tạo account.</summary>
    [AllowAnonymous]
    [HttpPost("oauth/github/verify")]
    public async Task<IActionResult> GithubVerify([FromBody] OAuthGithubVerifyRequestDto request)
    {
        var result = await _authService.VerifyGithubTokenAsync(request);
        return SuccessResp.Ok(result);
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/oauth/github
    // Bước 2 GitHub OAuth flow: đăng nhập (returning user) hoặc tạo user + profile (new user).
    // Field GithubUrl trong HRProfile/CandidateProfile được auto-fill từ tài khoản GitHub.
    // ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Đăng nhập / đăng ký bằng GitHub OAuth. Với user mới phải kèm IntendedRole + các field
    /// profile (CompanyId/CompanyName/JobTitle cho HR; TargetRole/SeniorityLevel/TechStack cho
    /// Candidate) — giống /oauth/google.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("oauth/github")]
    public async Task<IActionResult> GithubLogin([FromBody] OAuthGithubLoginRequestDto request)
    {
        var result = await _authService.GithubOAuthLoginAsync(request);
        return SuccessResp.Ok(result);
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/refresh-token   (SCRUM-155)
    // ────────────────────────────────────────────────────────────────
    /// <summary>Cấp access token mới bằng refresh token (xoay vòng).</summary>
    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        var result = await _authService.RefreshTokenAsync(request);
        return SuccessResp.Ok(result);
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/logout   (SCRUM-155)
    // ────────────────────────────────────────────────────────────────
    /// <summary>Đăng xuất — thu hồi refresh token.</summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request)
    {
        await _authService.LogoutAsync(request.RefreshToken);
        return SuccessResp.NoContent();
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/register/hr   (SCRUM-147 + 148)
    // ────────────────────────────────────────────────────────────────
    /// <summary>Đăng ký tài khoản HR Manager. Gửi email xác minh, chưa cấp JWT.</summary>
    [AllowAnonymous]
    [HttpPost("register/hr")]
    public async Task<IActionResult> RegisterHR([FromBody] RegisterHRRequestDto request)
    {
        await _authService.RegisterHRAsync(request);
        return SuccessResp.Created(
            "Đăng ký thành công! Vui lòng kiểm tra email để xác minh tài khoản.");
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/register/candidate   (SCRUM-150)
    // ────────────────────────────────────────────────────────────────
    /// <summary>Đăng ký Candidate — auto-verified, trả JWT ngay (AC-05).</summary>
    [AllowAnonymous]
    [HttpPost("register/candidate")]
    public async Task<IActionResult> RegisterCandidate([FromBody] RegisterCandidateRequestDto request)
    {
        await _authService.RegisterCandidateAsync(request);
        return SuccessResp.Created(
            "Đăng ký thành công! Vui lòng kiểm tra email để xác minh tài khoản.");
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/verify-email   (SCRUM-151)
    // ────────────────────────────────────────────────────────────────
    /// <summary>Xác minh email bằng mã OTP 6 chữ số (AC-01..03).</summary>
    [AllowAnonymous]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto request)
    {
        await _authService.VerifyEmailAsync(request);
        return SuccessResp.Ok("Email đã được xác minh thành công. Bạn có thể đăng nhập ngay bây giờ.");
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/resend-verification   (SCRUM-151 AC-04)
    // ────────────────────────────────────────────────────────────────
    /// <summary>Gửi lại email xác minh. Luôn trả success.</summary>
    [AllowAnonymous]
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto request)
    {
        await _authService.ResendVerificationEmailAsync(request);
        return SuccessResp.Ok(
            "Nếu email của bạn chưa được xác minh, chúng tôi đã gửi mã OTP mới. Vui lòng kiểm tra hộp thư.");
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/forgot-password   (SCRUM-157)
    // AC-01..02
    // ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Gửi email đặt lại mật khẩu. Luôn trả success bất kể email có tồn tại hay không (AC-02).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        await _authService.ForgotPasswordAsync(request);
        return SuccessResp.Ok(
            "Nếu email của bạn tồn tại trong hệ thống, chúng tôi đã gửi đường dẫn đặt lại mật khẩu. Vui lòng kiểm tra hộp thư.");
    }

    // ────────────────────────────────────────────────────────────────
    // POST api/auth/reset-password   (SCRUM-157)
    // AC-03..05
    // ────────────────────────────────────────────────────────────────
    /// <summary>Đặt lại mật khẩu bằng token nhận được qua email.</summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        await _authService.ResetPasswordAsync(request);
        return SuccessResp.Ok("Mật khẩu đã được đặt lại thành công. Vui lòng đăng nhập lại.");
    }
}
