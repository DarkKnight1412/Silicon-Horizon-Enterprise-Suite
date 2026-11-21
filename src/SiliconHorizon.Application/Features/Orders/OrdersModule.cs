using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Orders;

/// <summary>
/// Đăng ký DI của module Orders + Payments (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>),
/// được <c>AddApplication()</c> gọi lại.
/// </summary>
/// <remarks>
/// Đơn hàng và thanh toán cùng nằm trong file proc Database/03_sp_orders_payments.sql và dùng chung
/// <see cref="OrderAccessGuard"/> để ép phạm vi chi nhánh/khách hàng, nên gộp một module.
/// </remarks>
public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        // Đơn hàng (usp_Order_*): tạo đơn online/tại quầy, danh sách, chi tiết, chuyển trạng thái, hủy.
        services.AddScoped<IOrderService, OrderService>();

        // Thanh toán (usp_Payment_*, usp_Customer_GetLedger): thu tiền, callback cổng, hoàn tiền, sao kê công nợ.
        services.AddScoped<IPaymentService, PaymentService>();

        // Chốt chặn phạm vi dữ liệu dùng chung cho hai service trên (khách theo CustomerId, nhân viên theo BranchId).
        services.AddScoped<OrderAccessGuard>();

        return services;
    }
}
