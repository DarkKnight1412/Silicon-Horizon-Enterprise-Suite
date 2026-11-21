using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Reports;

/// <summary>
/// Đăng ký DI của module Báo cáo (Database/06_sp_hr_audit_reports.sql, nhóm <c>usp_Report_*</c>),
/// được <c>AddApplication()</c> gọi lại (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>).
/// </summary>
/// <remarks>
/// Tên hàm giữ theo ghi chú đã có trong <c>Application/DependencyInjection.cs</c> (<c>AddReportModule()</c>)
/// dù thư mục module là <c>Reports</c>. Vòng đời <b>Scoped</b> vì service dùng <c>ISqlExecutor</c> và
/// <c>ICurrentUser</c> (đều Scoped).
/// </remarks>
public static class ReportsModule
{
    public static IServiceCollection AddReportModule(this IServiceCollection services)
    {
        // 5 báo cáo: doanh thu, tồn kho, serial theo trạng thái, công nợ, bảo hành.
        services.AddScoped<IReportService, ReportService>();

        // Chốt chặn phạm vi chi nhánh (Quản lý/thủ kho bị ép BranchId, Admin toàn hệ thống).
        services.AddScoped<ReportAccessGuard>();

        return services;
    }
}
