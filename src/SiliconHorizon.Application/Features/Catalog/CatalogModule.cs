using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Catalog;

/// <summary>
/// Đăng ký DI của module Catalog. Mỗi module có đúng một lớp đăng ký như thế này
/// (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>) và được <c>AddApplication()</c> gọi lại.
/// </summary>
public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        // Use case danh mục (CRUD bằng EF Core — mục 10 của Database/README.md).
        services.AddScoped<ICategoryService, CategoryService>();

        // Sản phẩm + tương thích linh kiện (đi qua stored procedure).
        services.AddScoped<IProductService, ProductService>();

        // Tra cứu công khai danh mục/thương hiệu (EF Core).
        services.AddScoped<ICatalogReferenceService, CatalogReferenceService>();

        return services;
    }
}
