using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Customers;

/// <summary>
/// Đăng ký DI của module Customers — hồ sơ khách hàng (Admin CRUD) và sổ địa chỉ của khách
/// (Database/README.md mục 10: <c>/api/customers</c>, <c>/api/me/addresses</c>).
/// Được <c>AddApplication()</c> gọi lại.
/// </summary>
/// <remarks>
/// Vòng đời <b>Scoped</b> vì service dùng repository EF Core, <c>ICurrentUser</c> và <c>ISqlExecutor</c>.
/// Công nợ khách (<c>usp_Customer_GetLedger</c>) vẫn thuộc module Payments đã có.
/// </remarks>
public static class CustomerModule
{
    public static IServiceCollection AddCustomerModule(this IServiceCollection services)
    {
        // CRUD khách hàng (AD) — không xóa cứng, dùng isActive.
        services.AddScoped<ICustomerService, CustomerService>();

        // Sổ địa chỉ của khách (KH) — ép customerId từ token.
        services.AddScoped<ICustomerAddressService, CustomerAddressService>();

        return services;
    }
}
