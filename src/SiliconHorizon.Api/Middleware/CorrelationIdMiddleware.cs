using System.Diagnostics;
using Microsoft.Extensions.Primitives;

namespace SiliconHorizon.Api.Middleware;

/// <summary>
/// Gán mã tương quan cho mỗi request để tra log xuyên suốt (AUD-02, NFR-05).
/// <list type="number">
/// <item>Ưu tiên header <c>X-Correlation-Id</c> do client/gateway gửi lên.</item>
/// <item>Không có thì lấy <c>Activity.Current.TraceId</c> (W3C traceparent) rồi tới <c>HttpContext.TraceIdentifier</c>.</item>
/// <item>Trả lại cùng giá trị trong response header và gắn vào log scope; mọi lỗi trả về cũng kèm correlationId.</item>
/// </list>
/// Phải được đặt TRƯỚC mọi middleware khác để các middleware sau (kể cả exception handler) đều có mã này.
/// </summary>
public class CorrelationIdMiddleware
{
    /// <summary>Tên header trao đổi mã tương quan.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Khóa dùng cho <c>HttpContext.Items</c>.</summary>
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[ItemKey] = correlationId;

        // Không sửa header nếu response đã bắt đầu ghi (ví dụ lỗi giữa chừng khi streaming).
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out StringValues headerValue)
            && !StringValues.IsNullOrEmpty(headerValue))
        {
            return headerValue.ToString();
        }

        return Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }
}
