using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Warranty;

/// <summary>
/// Đăng ký DI của module Bảo hành (Database/05_sp_warranty_returns_commission.sql — nhóm <c>usp_Warranty_*</c>),
/// được <c>AddApplication()</c> gọi lại (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>).
/// </summary>
/// <remarks>
/// Tra cứu bảo hành theo serial (<c>GET /api/v1/warranty/lookup?serial=</c>) dùng proc <c>usp_Serial_Lookup</c>
/// của module Kho/Serial nên tái sử dụng <c>IInventoryService</c>, không đăng ký thêm service ở đây.
/// </remarks>
public static class WarrantyModule
{
    public static IServiceCollection AddWarrantyModule(this IServiceCollection services)
    {
        // Tiếp nhận, chuyển trạng thái, đính kèm, danh sách và chi tiết phiếu bảo hành.
        services.AddScoped<IWarrantyService, WarrantyService>();

        // Chốt chặn phạm vi dữ liệu dùng chung (nhân viên theo BranchId, khách theo CustomerId).
        services.AddScoped<WarrantyAccessGuard>();

        return services;
    }
}