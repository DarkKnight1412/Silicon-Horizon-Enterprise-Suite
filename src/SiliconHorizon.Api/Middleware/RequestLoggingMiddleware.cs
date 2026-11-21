using System.Diagnostics;
using SiliconHorizon.Application.Abstractions.Security;

namespace SiliconHorizon.Api.Middleware;

/// <summary>
/// Ghi log một dòng cho mỗi request (method, path, status, thời gian xử lý, người gọi, mã tương quan).
/// Đặt SAU exception handler để vẫn ghi được cả request lỗi, và TRƯỚC authentication để đo được toàn bộ thời gian.
/// Không thay thế nhật ký nghiệp vụ AUD-01 (việc đó do <c>usp_Audit_Write</c>).
/// </summary>
public class RequestLoggingMiddleware
{
    private static readonly string[] IgnoredPathPrefixes = ["/api/v1/health", "/swagger", "/favicon.ico"];

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        if (ShouldIgnore(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.Value;
        var userId = currentUser.UserId?.ToString() ?? "anonymous";
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey];

        // Ghi log khi response đã hoàn tất: lúc đó StatusCode mới là giá trị cuối cùng,
        // kể cả khi lỗi đã được ApiExceptionHandler ghi đè phía sau.
        context.Response.OnCompleted(() =>
        {
            _logger.LogInformation(
                "HTTP {Method} {Path} => {StatusCode} ({ElapsedMs} ms, user={UserId}, correlationId={CorrelationId})",
                method,
                path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                userId,
                correlationId);

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static bool ShouldIgnore(PathString path)
        => IgnoredPathPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}
