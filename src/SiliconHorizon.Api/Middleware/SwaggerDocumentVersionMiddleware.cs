using System.Text;
using System.Text.RegularExpressions;

namespace SiliconHorizon.Api.Middleware;

/// <summary>
/// Ghi đè trường <c>openapi</c> trong tài liệu Swagger, ví dụ Swashbuckle 6.6.2 sinh <c>"3.0.4"</c>
/// và ta muốn trả về <c>"3.0.1"</c>.
/// <para>
/// Lý do cần: Swashbuckle chốt phiên bản OpenAPI theo package <c>Microsoft.OpenApi</c>, không có tuỳ chọn cấu hình;
/// một số công cụ kiểm tra/cổng API chỉ chấp nhận 3.0.1.
/// Giá trị đích đọc từ cấu hình <c>Swagger:OpenApiVersion</c> (mặc định 3.0.1).
/// </para>
/// <para>Muốn bỏ: xoá dòng <c>app.UseMiddleware&lt;SwaggerDocumentVersionMiddleware&gt;();</c> trong Program.cs.</para>
/// </summary>
public sealed partial class SwaggerDocumentVersionMiddleware
{
    /// <summary>Đường dẫn tài liệu Swagger cần ghi đè phiên bản.</summary>
    public const string DocumentPath = "/swagger/v1/swagger.json";

    private readonly RequestDelegate _next;
    private readonly string _targetVersion;

    public SwaggerDocumentVersionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _targetVersion = configuration["Swagger:OpenApiVersion"] ?? "3.0.1";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method)
            || !context.Request.Path.Equals(DocumentPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var originalBody = context.Response.Body;

        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            // Middleware UseSwagger viết tài liệu vào buffer để ta sửa trước khi trả cho client.
            await _next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;
        var json = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();

        // Chỉ sửa khi thật sự có tài liệu hợp lệ (tránh đụng vào response lỗi 404/401...).
        if (context.Response.StatusCode == StatusCodes.Status200OK && json.Contains("\"openapi\"", StringComparison.Ordinal))
        {
            json = OpenApiVersionRegex().Replace(json, $"\"openapi\": \"{_targetVersion}\"");
        }

        var payload = Encoding.UTF8.GetBytes(json);
        context.Response.ContentLength = payload.Length;

        await originalBody.WriteAsync(payload, context.RequestAborted);
    }

    /// <summary>Khớp <c>"openapi": "3.0.4"</c> với khoảng trắng tuỳ ý.</summary>
    [GeneratedRegex("\"openapi\"\\s*:\\s*\"[0-9]+\\.[0-9]+\\.[0-9]+\"")]
    private static partial Regex OpenApiVersionRegex();
}
