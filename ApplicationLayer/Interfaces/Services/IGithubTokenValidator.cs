using ApplicationLayer.DTOs.Auth;

namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Abstraction để đổi GitHub Authorization Code lấy thông tin tài khoản. Tách khỏi HttpClient
/// cụ thể để AuthService có thể unit-test, giống IGoogleTokenValidator.
/// </summary>
public interface IGithubTokenValidator
{
    /// <summary>
    /// Đổi "code" (Authorization Code do GitHub redirect về FE) lấy access token, sau đó gọi
    /// GitHub REST API để lấy thông tin tài khoản + email chính đã xác minh.
    /// Throw UnauthorizedException nếu code sai/hết hạn, BadRequestException nếu tài khoản
    /// GitHub chưa có email nào được xác minh.
    /// </summary>
    Task<GithubAccountInfo> ValidateAsync(string code);
}
