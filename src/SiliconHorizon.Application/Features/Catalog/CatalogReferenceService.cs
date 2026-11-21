using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Features.Catalog.Dto;
using SiliconHorizon.Domain.Entities.Catalog;

namespace SiliconHorizon.Application.Features.Catalog;

/// <summary>
/// Dữ liệu tra cứu công khai (danh mục, thương hiệu). Đọc bằng EF Core vì là bảng nhỏ, ít thay đổi —
/// đúng ghi chú "GET /api/categories, /api/brands → EF (đọc bảng)" trong Database/README.md mục 3.1.
/// </summary>
public interface ICatalogReferenceService
{
    Task<IReadOnlyList<ReferenceItemDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReferenceItemDto>> GetBrandsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Hiện thực tra cứu danh mục/thương hiệu.</summary>
public sealed class CatalogReferenceService : ICatalogReferenceService
{
    private readonly IReadRepository<Category> _categories;
    private readonly IReadRepository<Brand> _brands;

    public CatalogReferenceService(IReadRepository<Category> categories, IReadRepository<Brand> brands)
    {
        _categories = categories;
        _brands = brands;
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(cancellationToken);

        return categories
            .Where(category => category.IsActive)
            .OrderBy(category => category.Code)
            .Select(category => new ReferenceItemDto(category.Id, category.Code, category.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetBrandsAsync(
        CancellationToken cancellationToken = default)
    {
        var brands = await _brands.ListAsync(cancellationToken);

        return brands
            .Where(brand => brand.IsActive)
            .OrderBy(brand => brand.Name)
            .Select(brand => new ReferenceItemDto(brand.Id, null, brand.Name))
            .ToList();
    }
}
