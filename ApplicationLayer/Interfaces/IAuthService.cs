using ApplicationLayer.DTOs.Auth;

namespace ApplicationLayer.Interfaces;

public interface IAuthService
{
    // ── Login (SCRUM-154/155) ──────────────────────────
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    Task<LoginResponseDto> OAuthLoginAsync(OAuthLoginRequestDto request);
    Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
    Task LogoutAsync(string refreshToken);

    // ── Registration (SCRUM-147, 150) ─────────────────
    Task RegisterHRAsync(RegisterHRRequestDto request);
    Task<LoginResponseDto> RegisterCandidateAsync(RegisterCandidateRequestDto request);

    // ── Email Verification (SCRUM-151, 148) ───────────
    Task VerifyEmailAsync(VerifyEmailDto request);
    Task ResendVerificationEmailAsync(ResendVerificationDto request);

    // ── Forgot / Reset Password (SCRUM-157) ───────────
    Task ForgotPasswordAsync(ForgotPasswordRequestDto request);
    Task ResetPasswordAsync(ResetPasswordRequestDto request);
}
