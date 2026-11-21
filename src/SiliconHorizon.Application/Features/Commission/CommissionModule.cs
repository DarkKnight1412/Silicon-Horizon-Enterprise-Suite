using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Commission;

/// <summary>
/// Đăng ký DI của module Hoa hồng (Database/05_sp_warranty_returns_commission.sql — nhóm <c>usp_Commission_*</c>),
/// được <c>AddApplication()</c> gọi lại (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>).
/// </summary>
public static class CommissionModule
{
    public static IServiceCollection AddCommissionModule(this IServiceCollection services)
    {
        // Tổng hợp hoa hồng, điều chỉnh thủ công, chốt kỳ hoa hồng.
        services.AddScoped<ICommissionService, CommissionService>();

        // Chốt chặn phạm vi dữ liệu (nhân viên theo EmployeeId, Quản lý theo BranchId, Admin toàn hệ thống).
        services.AddScoped<CommissionAccessGuard>();

        return services;
    }
}