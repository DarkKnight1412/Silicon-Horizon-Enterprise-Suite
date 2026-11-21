using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Pricing.Dto;

namespace SiliconHorizon.Application.Features.Pricing;

/// <summary>
/// Use case giá bậc thang theo nhóm khách — CRUD bằng EF Core trên <c>dbo.PriceTiers</c>
/// (Database/README.md mục 10: <c>/api/admin/products/{id}/price-tiers</c>; nghiệp vụ PRC-02).
/// </summary>
public interface IPriceTierService
{
    /// <summary>GET /api/v1/admin/products/{productId}/price-tiers — danh sách bậc giá của một sản phẩm.</summary>
    Task<PagedResult<PriceTierDto>> GetPagedAsync(
        long productId,
        PriceTierListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/products/{productId}/price-tiers/{tierId} — chi tiết một bậc giá.</summary>
    Task<PriceTierDto> GetByIdAsync(
        long productId,
        long tierId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/products/{productId}/price-tiers — thêm bậc giá.
    /// Ràng buộc: <c>minQuantity &gt; 1</c> (<c>CK_PriceTiers_MinQty</c>), <c>price &gt;= 0</c>,
    /// và bộ ba (productId, customerGroupId, minQuantity) không trùng (<c>UQ_PriceTiers</c>) → trùng trả 409.
    /// </summary>
    Task<PriceTierDto> CreateAsync(
        long productId,
        CreatePriceTierRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/admin/products/{productId}/price-tiers/{tierId} — sửa bậc giá (đổi giá ghi nhật ký).</summary>
    Task<PriceTierDto> UpdateAsync(
        long productId,
        long tierId,
        UpdatePriceTierRequest request,
        CancellationToken cancellationToken = default);
}
