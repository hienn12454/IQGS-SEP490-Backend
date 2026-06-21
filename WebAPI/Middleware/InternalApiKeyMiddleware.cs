using ApplicationLayer.Settings;
using Microsoft.Extensions.Options;

namespace WebAPI.Middleware;

/// <summary>
/// Bảo vệ route /internal/* bằng X-Internal-Api-Key — dùng cho RAG callback.
/// </summary>
public class InternalApiKeyMiddleware
{
    private const string HeaderName = "X-Internal-Api-Key";
    private readonly RequestDelegate _next;
    private readonly InternalApiSettings _settings;

    public InternalApiKeyMiddleware(RequestDelegate next, IOptions<InternalApiSettings> settings)
    {
        _next = next;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_settings.Key))
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { Code = 500, Error = "InternalApi:Key chưa được cấu hình." });
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            providedKey != _settings.Key)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { Code = 401, Error = "Invalid internal API key." });
            return;
        }

        await _next(context);
    }
}
