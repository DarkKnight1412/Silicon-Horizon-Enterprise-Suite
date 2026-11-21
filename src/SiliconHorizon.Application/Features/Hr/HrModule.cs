using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Hr;

/// <summary>
/// Đăng ký DI của module Nhân sự — xếp ca và chấm công
/// (Database/06_sp_hr_audit_reports.sql, nhóm <c>usp_Schedule_*</c>/<c>usp_Attendance_*</c>),
/// được <c>AddApplication()</c> gọi lại (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>).
/// </summary>
/// <remarks>
/// Vòng đời <b>Scoped</b> vì service dùng <c>ISqlExecutor</c>, repository và <c>ICurrentUser</c> (đều Scoped).
/// </remarks>
public static class HrModule
{
    public static IServiceCollection AddHrModule(this IServiceCollection services)
    {
        // Bảng xếp ca, chấm công vào/ra, điều chỉnh công, bảng công tháng.
        services.AddScoped<IHrService, HrService>();

        // Chốt chặn phạm vi dữ liệu (nhân viên theo EmployeeId, Quản lý theo BranchId, Admin toàn hệ thống).
        services.AddScoped<HrAccessGuard>();

        return services;
    }
}
