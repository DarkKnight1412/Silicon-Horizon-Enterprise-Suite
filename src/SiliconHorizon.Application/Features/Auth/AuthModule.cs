using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Auth;

/// <summary>Đăng ký DI của module Auth (quy ước &lt;Module&gt;Module.cs trong thư mục Features).</summary>
public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();

        // Quản lý tài khoản/vai trò/quyền bằng EF Core đã được hiện thực ở module Admin
        // (Features/Admin: IRoleService, IPermissionService, IUserRoleService) — xem AdminModule.

        return services;
    }
}
