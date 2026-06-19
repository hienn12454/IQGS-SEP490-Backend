using DomainLayer.Common;
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
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _config;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env,
        IConfiguration config)
    {
        _next = next;
        _logger = logger;
        _env = env;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (StructuredHttpException ex)
        {
            _logger.LogWarning("HTTP {StatusCode}: {Stage} {Message}", ex.HttpStatusCode, ex.Payload.Stage, ex.Message);
            await WriteStructuredAsync(context, ex.HttpStatusCode, ex.Payload);
        }
        catch (BaseHttpException ex)
        {
            _logger.LogWarning("HTTP {StatusCode}: {Message}", ex.HttpStatusCode, ex.Message);
            await WriteJsonAsync(context, ex.HttpStatusCode, ex.Message, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception at {Path}", context.Request.Path);

            var userMessage = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.";
            await WriteJsonAsync(context, StatusCodes.Status500InternalServerError, userMessage, ex);
        }
    }

    private static async Task WriteStructuredAsync(HttpContext context, int statusCode, StructuredErrorPayload payload)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            code = statusCode,
            error = payload.Error,
            detail = payload.Detail ?? payload.Errors.FirstOrDefault() ?? payload.Error,
            stage = payload.Stage,
            source = payload.Source,
            errors = payload.Errors
        });
    }

    private async Task WriteJsonAsync(HttpContext context, int statusCode, string message, Exception? ex = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var showDetail = _env.IsDevelopment()
            || string.Equals(_config["DetailedErrors"], "true", StringComparison.OrdinalIgnoreCase);

        if (!showDetail || ex is null || ex is BaseHttpException)
        {
            await context.Response.WriteAsJsonAsync(new { code = statusCode, error = message });
            return;
        }

        await context.Response.WriteAsJsonAsync(new
        {
            code = statusCode,
            error = message,
            detail = GetRootMessage(ex),
            exceptionType = ex.GetType().Name,
            stage = ex.Data.Contains("stage") ? ex.Data["stage"]?.ToString() : null,
            innerError = ex.InnerException?.Message,
            rootError = GetRootMessage(ex),
            source = ex.Source
        });
    }

    private static string GetRootMessage(Exception ex)
    {
        while (ex.InnerException is not null)
            ex = ex.InnerException;
        return ex.Message;
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
