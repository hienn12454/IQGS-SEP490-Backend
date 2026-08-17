using System.Net.Http.Headers;
using System.Text.Json;
using ApplicationLayer.DTOs.Auth;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace ApplicationLayer.Services;

/// <summary>
/// Concrete validator cho GitHub OAuth. Khác Google (ID Token là JWT ký sẵn, verify offline),
/// GitHub dùng Authorization Code flow: FE gửi lên "code", server phải đổi code đó lấy
/// access_token (cần Client Secret — bắt buộc thực hiện phía server) rồi gọi GitHub REST API
/// để lấy thông tin tài khoản. Đặt ở ApplicationLayer theo convention sẵn có của project
/// (giống GoogleTokenValidator).
/// </summary>
public class GithubTokenValidator : IGithubTokenValidator
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;

    // GitHub Authorization Code chỉ đổi lấy access_token được ĐÚNG 1 LẦN với GitHub, nhưng FE
    // flow gọi backend 2 lượt với cùng 1 code (POST .../verify rồi POST .../oauth/github).
    // Cache kết quả theo code trong ít phút để lượt gọi thứ 2 không phải đổi code với GitHub
    // lần nữa (sẽ bị GitHub từ chối "bad_verification_code").
    private static readonly TimeSpan CodeResultTtl = TimeSpan.FromMinutes(5);

    public GithubTokenValidator(HttpClient http, IConfiguration config, IMemoryCache cache)
    {
        _http = http;
        _config = config;
        _cache = cache;
    }

    public async Task<GithubAccountInfo> ValidateAsync(string code)
    {
        var cacheKey = $"github-oauth-code:{code}";
        if (_cache.TryGetValue(cacheKey, out GithubAccountInfo? cached) && cached != null)
            return cached;

        var clientId = _config["GithubOAuth:ClientId"];
        var clientSecret = _config["GithubOAuth:ClientSecret"];

        // Dùng IsNullOrWhiteSpace thay vì "?? throw": appsettings.json để sẵn ClientSecret là
        // chuỗi rỗng "" (placeholder, giá trị thật set qua env var GithubOAuth__ClientSecret trên
        // Azure) — "" vẫn không phải null nên "??" không bắt được, sẽ âm thầm gửi secret rỗng
        // sang GitHub và bị GitHub từ chối, hiện ra 401 chung chung không rõ nguyên nhân.
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("GithubOAuth:ClientId chưa được cấu hình.");
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("GithubOAuth:ClientSecret chưa được cấu hình.");

        var accessToken = await ExchangeCodeForTokenAsync(code, clientId, clientSecret);
        var (id, login, name, avatarUrl, htmlUrl) = await FetchGithubUserAsync(accessToken);
        var (email, emailVerified) = await FetchPrimaryVerifiedEmailAsync(accessToken);

        if (string.IsNullOrWhiteSpace(email))
            throw new BadRequestException(
                "Tài khoản GitHub của bạn chưa có email nào được xác minh. Vui lòng xác minh email trong Settings > Emails trên GitHub rồi thử lại.");

        var account = new GithubAccountInfo
        {
            Id = id,
            Login = login,
            Name = name,
            Email = email,
            AvatarUrl = avatarUrl,
            ProfileUrl = htmlUrl,
            EmailVerified = emailVerified
        };

        _cache.Set(cacheKey, account, CodeResultTtl);
        return account;
    }

    // ────────────────────────────────────────────────────────────────
    // Bước 1: đổi Authorization Code lấy access_token
    // POST https://github.com/login/oauth/access_token
    // ────────────────────────────────────────────────────────────────
    private async Task<string> ExchangeCodeForTokenAsync(string code, string clientId, string clientSecret)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code
        });

        HttpResponseMessage res;
        try
        {
            res = await _http.SendAsync(req);
        }
        catch (Exception ex) when (ex is not HttpRequestException
                                       && ex is not TaskCanceledException
                                       && ex is not OperationCanceledException)
        {
            throw new UnauthorizedException(
                "Không thể xác thực với GitHub. Vui lòng thử đăng nhập lại.");
        }

        if (!res.IsSuccessStatusCode)
            throw new UnauthorizedException(
                "Mã xác thực GitHub không hợp lệ hoặc đã hết hạn. Vui lòng thử đăng nhập lại.");

        await using var stream = await res.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        // GitHub trả HTTP 200 kể cả khi code sai — lỗi nằm trong body ("error": "bad_verification_code"...).
        if (!root.TryGetProperty("access_token", out var tokenProp)
            || tokenProp.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(tokenProp.GetString()))
            throw new UnauthorizedException(
                "Mã xác thực GitHub không hợp lệ hoặc đã hết hạn. Vui lòng thử đăng nhập lại.");

        return tokenProp.GetString()!;
    }

    // ────────────────────────────────────────────────────────────────
    // Bước 2: lấy thông tin tài khoản — GET https://api.github.com/user
    // ────────────────────────────────────────────────────────────────
    private async Task<(string Id, string Login, string? Name, string? AvatarUrl, string HtmlUrl)>
        FetchGithubUserAsync(string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.UserAgent.ParseAdd("IQGS-Backend");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
            throw new UnauthorizedException(
                "Không thể lấy thông tin tài khoản GitHub. Vui lòng thử đăng nhập lại.");

        await using var stream = await res.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        var id = root.GetProperty("id").GetInt64().ToString();
        var login = root.TryGetProperty("login", out var loginProp) ? loginProp.GetString() ?? "" : "";
        var name = root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
            ? nameProp.GetString()
            : null;
        var avatarUrl = root.TryGetProperty("avatar_url", out var avatarProp) && avatarProp.ValueKind == JsonValueKind.String
            ? avatarProp.GetString()
            : null;
        var htmlUrl = root.TryGetProperty("html_url", out var htmlProp) && htmlProp.ValueKind == JsonValueKind.String
            ? htmlProp.GetString()
            : null;
        htmlUrl ??= $"https://github.com/{login}";

        return (id, login, name, avatarUrl, htmlUrl);
    }

    // ────────────────────────────────────────────────────────────────
    // Bước 3: lấy email chính đã xác minh — GET https://api.github.com/user/emails
    // (/user.email có thể null nếu user để email private → cần scope "user:email").
    // Chỉ chấp nhận email đã "verified" — tránh rủi ro mạo danh bằng email chưa xác minh.
    // ────────────────────────────────────────────────────────────────
    private async Task<(string? Email, bool Verified)> FetchPrimaryVerifiedEmailAsync(string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.UserAgent.ParseAdd("IQGS-Backend");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
            return (null, false);

        await using var stream = await res.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return (null, false);

        string? anyVerified = null;
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var verified = entry.TryGetProperty("verified", out var v) && v.ValueKind == JsonValueKind.True;
            if (!verified) continue;

            var email = entry.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String
                ? e.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(email)) continue;

            var primary = entry.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.True;
            if (primary)
                return (email, true);

            anyVerified ??= email;
        }

        return anyVerified != null ? (anyVerified, true) : (null, false);
    }
}
