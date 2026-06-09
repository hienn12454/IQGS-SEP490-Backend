using ApplicationLayer.DTOs.Auth;

namespace ApplicationLayer.Interfaces.Services;

public interface IAuthService
{
    // ── Login ─────────────────────────────────────────────────────────
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);

    /// <summary>Verify-only: kiểm tra Google ID Token + tra user, KHÔNG tạo user, KHÔNG cấp JWT.</summary>
    Task<OAuthVerifyResponseDto> VerifyGoogleTokenAsync(OAuthVerifyRequestDto request);

    Task<LoginResponseDto> OAuthLoginAsync(OAuthLoginRequestDto request);
    Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
    Task LogoutAsync(string refreshToken);

    // ── Registration ──────────────────────────────────────────────────
    Task RegisterHRAsync(RegisterHRRequestDto request);
    Task RegisterCandidateAsync(RegisterCandidateRequestDto request);

    // ── Email Verification ────────────────────────────────────────────
    Task VerifyEmailAsync(VerifyEmailDto request);
    Task ResendVerificationEmailAsync(ResendVerificationDto request);

    // ── Forgot / Reset Password ───────────────────────────────────────
    Task ForgotPasswordAsync(ForgotPasswordRequestDto request);
    Task ResetPasswordAsync(ResetPasswordRequestDto request);
}
