using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Features.Catalog.Dto;
using SiliconHorizon.Domain.Entities.Catalog;
using SiliconHorizon.Domain.Entities.Common;

namespace SiliconHorizon.Application.Features.Catalog;

/// <summary>
/// Dữ liệu tra cứu công khai (danh mục, thương hiệu, chi nhánh). Đọc bằng EF Core vì là bảng nhỏ, ít
/// thay đổi — đúng ghi chú "GET /api/categories, /api/brands → EF (đọc bảng)" trong Database/README.md
/// mục 3.1. Chi nhánh dùng cùng cách để khách chọn NƠI NHẬN HÀNG khi đặt đơn.
/// </summary>
public interface ICatalogReferenceService
{
    Task<IReadOnlyList<ReferenceItemDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReferenceItemDto>> GetBrandsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Danh sách chi nhánh ĐANG HOẠT ĐỘNG (id, mã, tên, địa chỉ, điện thoại) — dùng cho lookup chọn
    /// chi nhánh ở trang đặt hàng. KHÔNG trả chi nhánh đã ngừng hoạt động vì proc bán hàng chỉ xét
    /// tồn của chi nhánh <c>IsActive = 1</c>.
    /// </summary>
    Task<IReadOnlyList<BranchReferenceDto>> GetBranchesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Hiện thực tra cứu danh mục/thương hiệu/chi nhánh.</summary>
public sealed class CatalogReferenceService : ICatalogReferenceService
{
    private readonly IReadRepository<Category> _categories;
    private readonly IReadRepository<Brand> _brands;
    private readonly IReadRepository<Branch> _branches;

    public CatalogReferenceService(
        IReadRepository<Category> categories,
        IReadRepository<Brand> brands,
        IReadRepository<Branch> branches)
    {
        _categories = categories;
        _brands = brands;
        _branches = branches;
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

    public async Task<IReadOnlyList<BranchReferenceDto>> GetBranchesAsync(
        CancellationToken cancellationToken = default)
    {
        var branches = await _branches.ListAsync(cancellationToken);

        return branches
            .Where(branch => branch.IsActive)
            .OrderBy(branch => branch.Name)
            .Select(BranchReferenceDto.FromEntity)
            .ToList();
    }
}

