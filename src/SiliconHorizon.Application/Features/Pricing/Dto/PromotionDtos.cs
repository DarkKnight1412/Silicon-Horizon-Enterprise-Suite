using SiliconHorizon.Domain.Entities.Pricing;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Pricing.Dto;

/// <summary>
/// Khuyến mãi trả ra API — CRUD bằng EF Core trên <c>dbo.Promotions</c>
/// (Database/README.md mục 10: <c>/api/admin/promotions</c>, nghiệp vụ PRC-03..06).
/// </summary>
public class PromotionDto
{
    public long Id { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>0 Product, 1 Brand, 2 Category — quyết định <see cref="ScopeId"/> trỏ tới bảng nào.</summary>
    public PromotionScopeType ScopeType { get; init; }

    /// <summary>ProductId / BrandId / CategoryId tùy theo <see cref="ScopeType"/>.</summary>
    public long ScopeId { get; init; }

    /// <summary>0 Percent (%), 1 Amount (VND).</summary>
    public DiscountType DiscountType { get; init; }

    public decimal DiscountValue { get; init; }

    public DateTime StartAt { get; init; }

    public DateTime EndAt { get; init; }

    /// <summary>Tổng số lượng áp dụng tối đa (chưa cưỡng chế trong <c>fn_ProductPrice</c>).</summary>
    public int? MaxQuantity { get; init; }

    /// <summary>Số lượng tối đa cho mỗi khách (chưa cưỡng chế trong <c>fn_ProductPrice</c>).</summary>
    public int? MaxPerCustomer { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static PromotionDto FromEntity(Promotion promotion) => new()
    {
        Id = promotion.Id,
        Name = promotion.Name,
        ScopeType = promotion.ScopeType,
        ScopeId = promotion.ScopeId,
        DiscountType = promotion.DiscountType,
        DiscountValue = promotion.DiscountValue,
        StartAt = promotion.StartAt,
        EndAt = promotion.EndAt,
        MaxQuantity = promotion.MaxQuantity,
        MaxPerCustomer = promotion.MaxPerCustomer,
        IsActive = promotion.IsActive,
        CreatedAt = promotion.CreatedAt,
        UpdatedAt = promotion.UpdatedAt
    };
}

/// <summary>Tham số lọc danh sách khuyến mãi — GET /api/v1/admin/promotions.</summary>
public class PromotionListQuery
{
    public string? Keyword { get; set; }

    public PromotionScopeType? ScopeType { get; set; }

    public long? ScopeId { get; set; }

    public bool? IsActive { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Yêu cầu tạo khuyến mãi — POST /api/v1/admin/promotions.
/// Ràng buộc: <c>discountValue &gt; 0</c>, <c>endAt &gt; startAt</c> (<c>CK_Promotions_*</c>) → sai trả 400.
/// </summary>
public class CreatePromotionRequest
{
    public string? Name { get; set; }

    public PromotionScopeType ScopeType { get; set; }

    /// <summary>Phải là Id tồn tại trong bảng tương ứng với <see cref="ScopeType"/>.</summary>
    public long ScopeId { get; set; }

    public DiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int? MaxQuantity { get; set; }

    public int? MaxPerCustomer { get; set; }
}

/// <summary>Yêu cầu cập nhật khuyến mãi — PUT /api/v1/admin/promotions/{id}.</summary>
public class UpdatePromotionRequest
{
    public string? Name { get; set; }

    public PromotionScopeType ScopeType { get; set; }

    public long ScopeId { get; set; }

    public DiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int? MaxQuantity { get; set; }

    public int? MaxPerCustomer { get; set; }
}

/// <summary>
/// Đổi trạng thái khuyến mãi — PATCH /api/v1/admin/promotions/{id}/status.
/// Đây là cách "xóa" đúng quy ước: <b>không xóa cứng</b> bản ghi đã phát sinh (Database/README.md mục 10).
/// </summary>
public record SetPromotionStatusRequest(bool IsActive);
