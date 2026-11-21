using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Pricing;

/// <summary>
/// Đăng ký DI của module Pricing (giá, khuyến mãi, voucher) — được <c>AddApplication()</c> gọi lại
/// (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>).
/// </summary>
/// <remarks>
/// Vòng đời <b>Scoped</b> vì các service dùng repository EF Core, <c>ISqlExecutor</c> và <c>ICurrentUser</c>
/// (đều Scoped — xem DEPENDENCY_INJECTION.md mục 3).
/// </remarks>
public static class PricingModule
{
    public static IServiceCollection AddPricingModule(this IServiceCollection services)
    {
        // POST /api/v1/vouchers/validate (usp_Voucher_Validate) + CRUD voucher (EF Core, mục 10 của README).
        services.AddScoped<IVoucherService, VoucherService>();

        // Nhóm khách hàng (bảng giá theo nhóm).
        services.AddScoped<ICustomerGroupService, CustomerGroupService>();

        // Giá bậc thang theo nhóm khách (PRC-02).
        services.AddScoped<IPriceTierService, PriceTierService>();

        // Khuyến mãi theo sản phẩm/thương hiệu/danh mục (PRC-03..06).
        services.AddScoped<IPromotionService, PromotionService>();

        return services;
    }
}
