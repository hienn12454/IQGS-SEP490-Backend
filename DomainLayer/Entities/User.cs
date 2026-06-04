namespace DomainLayer.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }           // nullable — OAuth users không có password
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }

    // ── Role (FK → Roles table) ───────────────────────
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    // ── Auth ──────────────────────────────────────────
    public bool IsEmailVerified { get; set; } = false;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutUntil { get; set; }
    public string Provider { get; set; } = "local";     // "local" | "google"
    public string? GoogleId { get; set; }

    // ── Forgot Password ───────────────────────────────
    public string? PasswordResetToken { get; set; }     // SHA-256 hash của raw token
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    // ── Email Verification ────────────────────────────
    public string? EmailVerificationToken { get; set; } // SHA-256 hash
    public DateTime? EmailVerificationTokenExpiresAt { get; set; }

    // ── Profile ───────────────────────────────────────
    public bool IsProfileComplete { get; set; } = false;
}
