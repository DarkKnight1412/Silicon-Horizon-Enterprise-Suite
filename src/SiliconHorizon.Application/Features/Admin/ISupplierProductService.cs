using SiliconHorizon.Application.Features.Admin.Dto;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Use case liên kết nhà cung cấp ↔ sản phẩm — bảng nối <c>dbo.SupplierProducts</c>
/// (Database/README.md mục 10, dòng "Nhà cung cấp": <c>suppliers</c>, <c>suppliers/{id}/products</c>).
/// </summary>
/// <remarks>
/// Danh mục nhà cung cấp đã có ở <c>Features/Inventory/SupplierService.cs</c>; phần còn thiếu của dòng này là
/// bảng nối (mã SKU phía nhà cung cấp) nên được bổ sung ở đây, không sửa lại module Inventory.
/// </remarks>
public interface ISupplierProductService
{
    /// <summary>GET /api/v1/admin/suppliers/{supplierId}/products — danh sách sản phẩm của nhà cung cấp.</summary>
    Task<IReadOnlyList<SupplierProductDto>> GetBySupplierAsync(
        long supplierId,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/suppliers/{supplierId}/products/{productId} — chi tiết một liên kết.</summary>
    Task<SupplierProductDto> GetByIdAsync(
        long supplierId,
        long productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/suppliers/{supplierId}/products — thêm liên kết.
    /// Trùng cặp (supplierId, productId) trả 409 vì đây là khóa chính tổ hợp.
    /// </summary>
    Task<SupplierProductDto> CreateAsync(
        long supplierId,
        UpsertSupplierProductRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/admin/suppliers/{supplierId}/products/{productId} — sửa mã SKU nhà cung cấp.</summary>
    Task<SupplierProductDto> UpdateAsync(
        long supplierId,
        long productId,
        UpsertSupplierProductRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// DELETE /api/v1/admin/suppliers/{supplierId}/products/{productId} — gỡ liên kết.
    /// Xóa cứng an toàn: <c>SupplierProducts</c> là bảng nối thuần túy, không bảng nào tham chiếu tới nó.
    /// </summary>
    Task DeleteAsync(long supplierId, long productId, CancellationToken cancellationToken = default);
}
