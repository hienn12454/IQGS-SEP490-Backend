using ApplicationLayer.DTOs.Auth;

namespace ApplicationLayer.Interfaces.Services;

public interface IAuthService
{
    // ── Login ─────────────────────────────────────────────────────────
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    Task<LoginResponseDto> OAuthLoginAsync(OAuthLoginRequestDto request);
    Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
    Task LogoutAsync(string refreshToken);

    // ── Registration ──────────────────────────────────────────────────
    Task RegisterHRAsync(RegisterHRRequestDto request);
    Task<LoginResponseDto> RegisterCandidateAsync(RegisterCandidateRequestDto request);

    // ── Email Verification ────────────────────────────────────────────
    Task VerifyEmailAsync(VerifyEmailDto request);
    Task ResendVerificationEmailAsync(ResendVerificationDto request);

    // ── Forgot / Reset Password ───────────────────────────────────────
    Task ForgotPasswordAsync(ForgotPasswordRequestDto request);
    Task ResetPasswordAsync(ResetPasswordRequestDto request);
}
