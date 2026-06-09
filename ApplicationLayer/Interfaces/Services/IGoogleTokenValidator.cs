using ApplicationLayer.DTOs.Auth;

namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Abstraction để verify Google ID Token. Tách khỏi Google.Apis.Auth để
/// AuthService có thể unit-test và đổi provider mà không sửa logic.
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>
    /// Verify Google ID Token với client ID cấu hình sẵn. Throw UnauthorizedException
    /// (qua wrapper) nếu token sai/hết hạn.
    /// </summary>
    Task<GoogleAccountInfo> ValidateAsync(string idToken);
}
