using ApplicationLayer.DTOs.Auth;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Exceptions;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace ApplicationLayer.Services;

/// <summary>
/// Concrete validator dùng thư viện Google.Apis.Auth. Đặt ở ApplicationLayer
/// theo convention sẵn có của project (giống EmailService).
/// </summary>
public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly IConfiguration _config;

    public GoogleTokenValidator(IConfiguration config)
    {
        _config = config;
    }

    public async Task<GoogleAccountInfo> ValidateAsync(string idToken)
    {
        var googleClientId = _config["GoogleOAuth:ClientId"]
            ?? throw new InvalidOperationException("GoogleOAuth:ClientId chưa được cấu hình.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [googleClientId]
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch (Exception ex) when (ex is not HttpRequestException
                                       && ex is not TaskCanceledException
                                       && ex is not OperationCanceledException)
        {
            // Mọi lỗi liên quan đến token không hợp lệ (InvalidJwtException, FormatException,
            // ArgumentException, JsonException…) → 401.
            // Chỉ để lọt qua: lỗi mạng (HttpRequestException) → 500 xử lý ở middleware.
            throw new UnauthorizedException(
                "Phiên đăng nhập Google không hợp lệ hoặc đã hết hạn. Vui lòng thử đăng nhập lại.");
        }

        return new GoogleAccountInfo
        {
            Subject = payload.Subject,
            Email = payload.Email,
            Name = payload.Name,
            Picture = payload.Picture,
            EmailVerified = payload.EmailVerified
        };
    }
}
