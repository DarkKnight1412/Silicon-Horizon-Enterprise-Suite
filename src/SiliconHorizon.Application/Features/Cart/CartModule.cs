using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Cart;

/// <summary>
/// Đăng ký DI của module Cart (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>),
/// được <c>AddApplication()</c> gọi lại.
/// </summary>
public static class CartModule
{
    public static IServiceCollection AddCartModule(this IServiceCollection services)
    {
        // Giỏ hàng (usp_Cart_*): chủ giỏ là khách đã đăng nhập (customerId từ token)
        // hoặc khách vãng lai (sessionToken ở header X-Cart-Session).
        services.AddScoped<ICartService, CartService>();

        return services;
    }
}
