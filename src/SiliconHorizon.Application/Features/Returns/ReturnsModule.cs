using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Returns;

/// <summary>
/// Đăng ký DI của module Đổi trả (Database/05_sp_warranty_returns_commission.sql — nhóm <c>usp_Return_*</c>),
/// được <c>AddApplication()</c> gọi lại (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>).
/// </summary>
public static class ReturnsModule
{
    public static IServiceCollection AddReturnsModule(this IServiceCollection services)
    {
        // Tạo yêu cầu đổi trả, danh sách/chi tiết, duyệt/từ chối, nhận hàng & kiểm tra, hoàn tiền.
        services.AddScoped<IReturnService, ReturnService>();

        // Chốt chặn phạm vi dữ liệu dùng chung (khách theo CustomerId, nhân viên theo BranchId).
        services.AddScoped<ReturnAccessGuard>();

        return services;
    }
}