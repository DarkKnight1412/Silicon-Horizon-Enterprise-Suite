using SiliconHorizon.Domain.Entities.Catalog;
using SiliconHorizon.Domain.Entities.Inventory;

namespace SiliconHorizon.Application.Features.Admin.Dto;

/// <summary>
/// Liên kết nhà cung cấp ↔ sản phẩm — bảng nối <c>dbo.SupplierProducts</c>
/// (khóa chính tổ hợp <c>SupplierId + ProductId</c>; Database/README.md mục 10: <c>suppliers/{id}/products</c>).
/// </summary>
public class SupplierProductDto
{
    public long SupplierId { get; init; }

    public long ProductId { get; init; }

    /// <summary>Mã sản phẩm bên phía nhà cung cấp (varchar(50), tùy chọn).</summary>
    public string? SupplierSku { get; init; }

    /// <summary>Mã SKU nội bộ của sản phẩm, để quản trị đối chiếu khi lập phiếu nhập.</summary>
    public string? ProductSku { get; init; }

    public string? ProductName { get; init; }

    public static SupplierProductDto FromEntity(SupplierProduct link, Product? product) => new()
    {
        SupplierId = link.SupplierId,
        ProductId = link.ProductId,
        SupplierSku = link.SupplierSku,
        ProductSku = product?.Sku,
        ProductName = product?.Name
    };
}

/// <summary>Yêu cầu thêm/sửa liên kết nhà cung cấp ↔ sản phẩm.</summary>
public class UpsertSupplierProductRequest
{
    /// <summary><c>ProductId</c> trên route khi sửa; khi thêm mới lấy từ body.</summary>
    public long ProductId { get; set; }

    /// <summary>Mã sản phẩm theo nhà cung cấp (varchar(50), tùy chọn).</summary>
    public string? SupplierSku { get; set; }
}
