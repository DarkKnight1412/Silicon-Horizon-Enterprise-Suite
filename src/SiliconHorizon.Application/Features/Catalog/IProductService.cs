using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Catalog.Dto;

namespace SiliconHorizon.Application.Features.Catalog;

/// <summary>
/// Use case danh mục hàng hoá (PRD-01..14). Tất cả đều đi qua stored procedure
/// (xem Database/02_sp_foundation_catalog_cart.sql) — không truy vấn bảng trực tiếp.
/// </summary>
public interface IProductService
{
    /// <summary>GET /api/products — tìm kiếm, lọc chung + lọc theo thông số (usp_Product_Search).</summary>
    Task<PagedResult<ProductListItemDto>> SearchAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/products/{id} — header, ảnh, thông số, tồn từng chi nhánh (usp_Product_GetById).</summary>
    Task<ProductDetailDto> GetByIdAsync(
        long id,
        int? customerGroupId = null,
        CancellationToken cancellationToken = default);

    /// <summary>POST /api/admin/products — tạo sản phẩm kèm thông số và ảnh (usp_Product_Create).</summary>
    Task<long> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>PUT /api/admin/products/{id} — sửa sản phẩm, không đổi danh mục (usp_Product_Update).</summary>
    Task UpdateAsync(long id, UpdateProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>PATCH /api/admin/products/{id}/status — 0 Draft, 1 Active, 2 Discontinued (usp_Product_SetStatus).</summary>
    Task SetStatusAsync(long id, SetProductStatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>PUT /api/admin/products/{id}/prices/{groupId} — đặt giá theo nhóm khách (usp_ProductPrice_Upsert).</summary>
    Task UpsertPriceAsync(
        long id,
        int customerGroupId,
        UpsertProductPriceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>POST /api/compatibility/check — cảnh báo tương thích linh kiện (usp_Compat_Check, không chặn đặt hàng).</summary>
    Task<CompatibilityCheckResultDto> CheckCompatibilityAsync(
        CompatibilityCheckRequest request,
        CancellationToken cancellationToken = default);
}
