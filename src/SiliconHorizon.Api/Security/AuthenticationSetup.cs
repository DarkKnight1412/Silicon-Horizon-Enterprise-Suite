using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Infrastructure.Security;

namespace SiliconHorizon.Api.Security;

/// <summary>
/// Cấu hình xác thực (kiểm tra JWT Bearer) và phân quyền (theo vai trò + theo mã quyền).
/// Việc <b>phát hành</b> token nằm ở Infrastructure (<c>JwtTokenService</c>) với cùng khóa và cùng tham số kiểm tra.
/// <para>
/// Lưu ý thiết kế: KHÔNG bật fallback policy "mọi endpoint đều phải đăng nhập", vì BRD/Database/README.md
/// có nhiều endpoint Public (danh mục sản phẩm, giỏ hàng khách vãng lai). Endpoint cần bảo vệ phải gắn
/// <c>[Authorize]</c> / <c>[RequirePermission]</c> tường minh.
/// </para>
/// </summary>
public static class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Bind + kiểm tra cấu hình ngay khi khởi động: thiếu khóa hoặc khóa ngắn → báo lỗi rõ ràng
        // thay vì lỗi 401 khó hiểu lúc runtime.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException($"Thiếu section '{JwtOptions.SectionName}' trong cấu hình.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Giữ nguyên tên claim như khi phát hành (userId, customerId, employeeId, branchId, permission...).
                options.MapInboundClaims = false;

                // Dev chạy http:// nên không đòi metadata qua HTTPS.
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.SaveToken = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,

                    // Chỉ nhận access token: refresh token có audience riêng (Audience + ":refresh").
                    ValidAudience = jwtOptions.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = jwtOptions.ClockSkew,
                    NameClaimType = AuthClaimTypes.Username,
                    RoleClaimType = ClaimTypes.Role
                };
            });

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
