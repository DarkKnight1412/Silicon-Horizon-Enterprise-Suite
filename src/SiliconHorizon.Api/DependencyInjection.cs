using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using SiliconHorizon.Api.Constants;
using SiliconHorizon.Api.Exceptions;
using SiliconHorizon.Api.Middleware;
using SiliconHorizon.Api.Security;
using SiliconHorizon.Api.Storage;
using SiliconHorizon.Application;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Infrastructure;

namespace SiliconHorizon.Api;

/// <summary>
/// Composition root duy nhất của ứng dụng. Program.cs chỉ gọi một hàm này.
/// Thứ tự đăng ký: Application (use case) → Infrastructure (EF Core + Dapper) → thành phần của tầng Api.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // ── Tầng Application: use case theo module (DEPENDENCY_INJECTION.md mục 3) ──
        services.AddApplication();

        // ── Tầng Infrastructure: DbContext, ISqlExecutor, repository, UoW ─────────
        services.AddInfrastructure(configuration);

        // ── Xác thực & phân quyền (JWT Bearer + RBAC) ─────────────────────────────
        // Xem AUTHENTICATION_AND_AUTHORIZATION.md — gồm cả policy theo mã quyền (RequirePermission).
        services.AddJwtAuthentication(configuration, environment);

        // ── Tầng Api ──────────────────────────────────────────────────────────────
        services.AddHttpContextAccessor();                          // Singleton (do framework đăng ký)
        services.AddScoped<ICurrentUser, CurrentUser>();             // Scoped: đọc HttpContext của request hiện tại

        // ── Lưu ẢNH SẢN PHẨM do nhân viên upload (ghi vào wwwroot của web API) ────
        // Singleton: chỉ đọc cấu hình + thư mục web, không giữ trạng thái theo request.
        services.AddSingleton<IProductImageStorage, ProductImageStorage>();

        // ── Exception handling theo chuẩn .NET 8: IExceptionHandler + ProblemDetails ──
        // (MIDDLEWARE_AND_ERROR_HANDLING.md mục 2)
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var httpContext = context.HttpContext;

                // Mọi response lỗi đều có: instance, traceId (W3C) và correlationId để tra log.
                context.ProblemDetails.Instance ??= httpContext.Request.Path;
                context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

                if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId))
                {
                    context.ProblemDetails.Extensions["correlationId"] = correlationId;
                }
            };
        });

        // ── CORS: chỉ cần cho web khách hàng; API dùng JWT qua header nên không dùng cookie ──
        services.AddCors(options => options.AddPolicy(CorsPolicyNames.CustomerWeb, policy =>
        {
            var allowedOrigins = configuration
                .GetSection("Cors:AllowedOrigins")
                .GetChildren()
                .Select(section => section.Value)
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(origin => origin!)
                .ToArray();

            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            }
            else if (environment.IsDevelopment())
            {
                // Chưa cấu hình origin (chưa có web khách hàng) → mở cho môi trường Development.
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            }
        }));

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Silicon Horizon Enterprise Suite API",
                Version = "v1",
                Description = "Hệ thống Bán hàng & Quản lý Doanh nghiệp Linh kiện Điện tử (BRD)."
            });

            // TODO (khi bật JWT): thêm SecurityDefinition Bearer để gọi API có token trên Swagger.
        });

        return services;
    }
}
