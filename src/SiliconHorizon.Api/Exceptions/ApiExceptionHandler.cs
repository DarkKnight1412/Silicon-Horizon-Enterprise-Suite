using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SiliconHorizon.Api.Middleware;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Infrastructure.Sql;

namespace SiliconHorizon.Api.Exceptions;

/// <summary>
/// Xử lý tập trung mọi exception trong pipeline (thay cho middleware tự viết).
/// <list type="bullet">
/// <item><see cref="AppException"/> — lỗi nghiệp vụ, giữ nguyên thông điệp tiếng Việt (bao gồm lỗi 5000x đã dịch từ proc).</item>
/// <item><see cref="SqlException"/> — dịch qua <see cref="SqlErrorTranslator"/>; nếu không phải lỗi đã biết thì trả 500 và ghi log đầy đủ (không lộ chi tiết kỹ thuật).</item>
/// <item>Client hủy request — 499, chỉ ghi log ở mức Debug.</item>
/// <item>Còn lại — 500 với thông điệp chung.</item>
/// </list>
/// Đăng ký bằng <c>AddExceptionHandler&lt;ApiExceptionHandler&gt;()</c>; nội dung lỗi theo ProblemDetails
/// (xem MIDDLEWARE_AND_ERROR_HANDLING.md).
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    /// <summary>Mã 499 (client đóng kết nối trước khi server trả lời) — quy ước của nginx, dùng cho tiện tra log.</summary>
    private const int ClientClosedRequestStatusCode = 499;

    private const string GenericErrorMessage = "Lỗi hệ thống, vui lòng liên hệ quản trị viên.";

    private readonly ILogger<ApiExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, code, logLevel) = Map(exception, httpContext);

        LogException(exception, httpContext, statusCode, code, logLevel);

        if (httpContext.Response.HasStarted)
        {
            return true;
        }

        httpContext.Response.StatusCode = statusCode;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = code,
                Type = $"https://httpstatuses.com/{statusCode}"
            }
        });
    }

    private static (int StatusCode, string Title, string? Code, LogLevel LogLevel) Map(
        Exception exception,
        HttpContext httpContext)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return (ClientClosedRequestStatusCode, "Yêu cầu đã bị hủy bởi phía gọi.", "CLIENT-CLOSED", LogLevel.Debug);
        }

        // EF Core có thể bọc lỗi SQL trong RetryLimitExceededException/DbUpdateException,
        // nên phải mở dần inner exception để dịch được mã 5000x của stored procedure.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case AppException appException:
                    return (
                        appException.StatusCode,
                        appException.Message,
                        appException.Code,
                        appException.StatusCode >= 500 ? LogLevel.Error : LogLevel.Warning);

                case SqlException sqlException:
                    var translated = SqlErrorTranslator.Translate(sqlException);

                    return translated is null
                        ? (StatusCodes.Status500InternalServerError, GenericErrorMessage, $"SQL-{sqlException.Number}", LogLevel.Error)
                        : (translated.StatusCode, translated.Message, translated.Code,
                           translated.StatusCode >= 500 ? LogLevel.Error : LogLevel.Warning);
            }
        }

        return (StatusCodes.Status500InternalServerError, GenericErrorMessage, null, LogLevel.Error);
    }

    private void LogException(
        Exception exception,
        HttpContext httpContext,
        int statusCode,
        string? code,
        LogLevel logLevel)
    {
        if (logLevel == LogLevel.Debug)
        {
            // Hủy request là chuyện bình thường (người dùng chuyển trang, timeout) — không ghi log lỗi.
            _logger.LogDebug(
                "Request bị hủy: {Method} {Path} (correlationId={CorrelationId}).",
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                httpContext.Items[CorrelationIdMiddleware.ItemKey]);

            return;
        }

        _logger.Log(
            logLevel,
            exception,
            "Lỗi khi xử lý {Method} {Path} (status={StatusCode}, code={Code}, correlationId={CorrelationId}).",
            httpContext.Request.Method,
            httpContext.Request.Path.Value,
            statusCode,
            code,
            httpContext.Items[CorrelationIdMiddleware.ItemKey]);
    }
}
