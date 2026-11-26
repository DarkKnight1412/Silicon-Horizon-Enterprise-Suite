using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Infrastructure.Persistence;
using SiliconHorizon.Infrastructure.Persistence.Repositories;
using SiliconHorizon.Infrastructure.Security;
using SiliconHorizon.Infrastructure.Services;
using SiliconHorizon.Infrastructure.Sql;

namespace SiliconHorizon.Infrastructure;

/// <summary>Đăng ký toàn bộ hạ tầng (EF Core + SQL Server, Dapper, repository) vào DI container.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Ví dụ trong Program.cs:
    /// <code>builder.Services.AddInfrastructure(builder.Configuration);</code>
    /// Chuỗi kết nối đọc từ <c>ConnectionStrings:DefaultConnection</c> (appsettings hoặc biến môi trường).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Thiếu ConnectionStrings:DefaultConnection. Xem src/SiliconHorizon.Api/appsettings.json.");
        }

        // EF Core: chỉ dùng cho bảng danh mục/cấu hình (28 bảng) và cho thao tác CRUD đơn giản.
        services.AddDbContext<AppDbContext>(options => options
            .UseSqlServer(connectionString, sql => sql
                .MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
                .EnableRetryOnFailure(3)));

        // ── Dapper: gọi stored procedure của các module giao dịch ─────────────
        // Singleton: chỉ giữ chuỗi kết nối; mỗi lần gọi tạo một connection mới rồi đóng.
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

        // Scoped: phụ thuộc ICurrentUser (Scoped) để truyền IP/User-Agent vào sp_set_session_context.
        services.AddScoped<ISqlExecutor, DapperSqlExecutor>();

        // Tiện ích dùng chung.
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Xác minh ID token Google cho "Đăng nhập bằng Google" (đọc Google:ClientId trong cấu hình).
        services.AddSingleton<IGoogleTokenValidator, GoogleIdTokenValidator>();

        // ── Xác thực (AUTHENTICATION_AND_AUTHORIZATION.md) ─────────────────────
        // Singleton: hai service dưới đây không giữ trạng thái, chỉ đọc cấu hình JwtOptions.
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // ── Repository/Unit of Work dùng chung cho mọi entity EF Core ──────────
        // Scoped: bọc AppDbContext nên phải cùng vòng đời request với DbContext.
        services.AddScoped(typeof(IReadRepository<>), typeof(EfReadRepository<>));
        services.AddScoped(typeof(IWriteRepository<>), typeof(EfWriteRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }
}
