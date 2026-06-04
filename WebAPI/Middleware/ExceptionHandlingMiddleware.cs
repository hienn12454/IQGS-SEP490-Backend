using DomainLayer.Exceptions;

namespace WebAPI.Middleware;

/// <summary>
/// Global exception handler — bắt <see cref="BaseHttpException"/> và trả về JSON đúng HTTP status code.
/// Phải đăng ký TRƯỚC tất cả middleware khác trong pipeline.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BaseHttpException ex)
        {
            _logger.LogWarning("HTTP {StatusCode}: {Message}", ex.HttpStatusCode, ex.Message);
            await WriteJsonAsync(context, ex.HttpStatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteJsonAsync(context, StatusCodes.Status500InternalServerError,
                "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.");
        }
    }

    private static async Task WriteJsonAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { Code = statusCode, Error = message });
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
