using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Audit;

/// <summary>
/// Đăng ký DI của module Nhật ký hệ thống (Database/06_sp_hr_audit_reports.sql, nhóm <c>usp_Audit_*</c>),
/// được <c>AddApplication()</c> gọi lại (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>).
/// </summary>
/// <remarks>
/// Chỉ đăng ký use case ĐỌC nhật ký; <c>usp_Audit_Write</c> được các module nghiệp vụ gọi trong proc của họ
/// (và có thể gọi từ tầng API cho CRUD nhạy cảm) nên không cần service riêng.
/// </remarks>
public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        // Tra cứu nhật ký (usp_Audit_Search).
        services.AddScoped<IAuditService, AuditService>();

        // Chốt chặn phạm vi chi nhánh (Quản lý chỉ chi nhánh mình, Admin toàn hệ thống).
        services.AddScoped<AuditAccessGuard>();

        return services;
    }
}
