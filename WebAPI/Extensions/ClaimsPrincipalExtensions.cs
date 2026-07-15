using DomainLayer.Exceptions;
using System.Security.Claims;

namespace WebAPI.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Lấy UserId từ claim "sub"/NameIdentifier của JWT hiện tại.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdStr = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("Không xác định được người dùng từ token đăng nhập.");

        return userId;
    }
}
