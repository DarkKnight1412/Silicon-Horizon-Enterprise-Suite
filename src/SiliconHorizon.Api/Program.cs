using SiliconHorizon.Api;
using SiliconHorizon.Api.Constants;
using SiliconHorizon.Api.Middleware;
using SiliconHorizon.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Kiểm tra DI ngay khi khởi động: phát hiện service bị thiếu, vòng đời sai (scoped nằm trong singleton...).
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = context.HostingEnvironment.IsDevelopment();
});

// Composition root: gom DI của Application + Infrastructure + Api (xem DEPENDENCY_INJECTION.md).
builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

// ─────────────────────────────────────────────────────────────────────────────
// PIPELINE MIDDLEWARE — thứ tự CÓ Ý NGHĨA, không đổi tùy tiện.
// Chi tiết lý do từng vị trí: MIDDLEWARE_AND_ERROR_HANDLING.md mục 1.
// ─────────────────────────────────────────────────────────────────────────────

// 1) Mã tương quan cho mọi request — đặt trước tiên để mọi log/lỗi phía sau đều có mã này.
app.UseMiddleware<CorrelationIdMiddleware>();

// 2) Bắt mọi exception thành ProblemDetails (IExceptionHandler đã đăng ký trong AddApiServices).
app.UseExceptionHandler();

// 3) 404 / 401 / 403 / 405 cũng trả về cùng định dạng ProblemDetails.
app.UseStatusCodePages();

// 4) Ghi log request (log lúc response hoàn tất nên status luôn chính xác).
app.UseMiddleware<RequestLoggingMiddleware>();

// 5) HTTPS/HSTS: chỉ bật khi thật sự chạy sau TLS (môi trường Production).
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// 6) CORS phải trước authentication và trước endpoints.
app.UseCors(CorsPolicyNames.CustomerWeb);

// 7) Xác thực & phân quyền theo RBAC (BRD 2.2) — phải đúng thứ tự: authentication trước authorization.
app.UseAuthentication();
app.UseAuthorization();

// 8) Swagger chỉ mở ở môi trường Development.
if (app.Environment.IsDevelopment())
{
    // Chặn trình duyệt cache tài liệu/UI Swagger: nếu cache lại bản cũ (ví dụ tài liệu rỗng hoặc 404
    // từ lần chạy trước) thì Swagger UI sẽ báo lỗi "does not specify a valid version field".
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
        }

        await next();
    });

    // Ghi đè phiên bản OpenAPI (Swashbuckle sinh 3.0.4 → trả 3.0.1 theo cấu hình Swagger:OpenApiVersion).
    // PHẢI đứng TRƯỚC UseSwagger() để bọc được middleware sinh tài liệu.
    app.UseMiddleware<SwaggerDocumentVersionMiddleware>();

    app.UseSwagger();

    // Dùng URL TUYỆT ĐỐI cho tài liệu để tránh lỗi "does not specify a valid version field"
    // (Swagger UI tải nhầm tài liệu khi đường dẫn tương đối bị phân giải sai).
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "SiliconHorizon.Api v1");
        options.DocumentTitle = "Silicon Horizon Enterprise Suite API";
    });
}

// 9) Endpoints.
app.MapControllers();

// Kiểm tra API còn sống và kết nối được SQL Server.
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }))
   .WithTags("Health");

app.MapGet("/api/v1/health/db", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

    return canConnect
        ? Results.Ok(new { database = "connected" })
        : Results.Problem(
            "Không kết nối được SQL Server. Kiểm tra ConnectionStrings:DefaultConnection.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
})
.WithTags("Health");

app.Run();
