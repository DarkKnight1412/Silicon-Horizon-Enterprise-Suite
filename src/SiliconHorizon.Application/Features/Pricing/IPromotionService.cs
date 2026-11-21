using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Pricing.Dto;

namespace SiliconHorizon.Application.Features.Pricing;

/// <summary>
/// Use case khuyến mãi — CRUD bằng EF Core trên bảng cấu hình <c>dbo.Promotions</c>
/// (Database/README.md mục 10: <c>/api/admin/promotions</c>; nghiệp vụ PRC-03..06).
/// </summary>
public interface IPromotionService
{
    /// <summary>GET /api/v1/admin/promotions — danh sách khuyến mãi, có phân trang và lọc.</summary>
    Task<PagedResult<PromotionDto>> GetPagedAsync(
        PromotionListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/promotions/{id} — chi tiết khuyến mãi; không tồn tại → 404.</summary>
    Task<PromotionDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/promotions — tạo khuyến mãi.
    /// Sai <c>discountType</c>/<c>scopeType</c>, <c>discountValue &lt;= 0</c> hoặc <c>endAt &lt;= startAt</c> → 400;
    /// <c>scopeId</c> không tồn tại trong bảng tương ứng với <c>scopeType</c> → 400.
    /// </summary>
    Task<PromotionDto> CreateAsync(
        CreatePromotionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/admin/promotions/{id} — cập nhật khuyến mãi (cùng ràng buộc như khi tạo).</summary>
    Task<PromotionDto> UpdateAsync(
        long id,
        UpdatePromotionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PATCH /api/v1/admin/promotions/{id}/status — bật/tắt khuyến mãi.
    /// Thay cho xóa cứng: <c>fn_ProductPrice</c> chỉ áp dụng khuyến mãi đang <c>IsActive = 1</c>.
    /// </summary>
    Task SetStatusAsync(
        long id,
        SetPromotionStatusRequest request,
        CancellationToken cancellationToken = default);
}
