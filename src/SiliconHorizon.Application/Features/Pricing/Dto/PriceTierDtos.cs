using SiliconHorizon.Domain.Entities.Pricing;

namespace SiliconHorizon.Application.Features.Pricing.Dto;

/// <summary>
/// Giá bậc thang trả ra API — CRUD bằng EF Core trên <c>dbo.PriceTiers</c>
/// (Database/README.md mục 10: <c>/api/admin/products/{id}/price-tiers</c>, nghiệp vụ PRC-02).
/// </summary>
public class PriceTierDto
{
    public long Id { get; init; }

    public long ProductId { get; init; }

    public int CustomerGroupId { get; init; }

    /// <summary>Số lượng tối thiểu của bậc; ràng buộc <c>CK_PriceTiers_MinQty</c> yêu cầu lớn hơn 1.</summary>
    public int MinQuantity { get; init; }

    /// <summary>Giá bán (VND) khi mua từ <see cref="MinQuantity"/> sản phẩm trở lên.</summary>
    public decimal Price { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static PriceTierDto FromEntity(PriceTier tier) => new()
    {
        Id = tier.Id,
        ProductId = tier.ProductId,
        CustomerGroupId = tier.CustomerGroupId,
        MinQuantity = tier.MinQuantity,
        Price = tier.Price,
        CreatedAt = tier.CreatedAt,
        UpdatedAt = tier.UpdatedAt
    };
}

/// <summary>Tham số lọc danh sách giá bậc thang của một sản phẩm.</summary>
public class PriceTierListQuery
{
    /// <summary><c>null</c> = lấy bậc giá của mọi nhóm khách.</summary>
    public int? CustomerGroupId { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Yêu cầu tạo bậc giá — POST /api/v1/admin/products/{id}/price-tiers.
/// Bộ ba (productId, customerGroupId, minQuantity) là duy nhất (<c>UQ_PriceTiers</c>) → trùng trả 409.
/// </summary>
public class CreatePriceTierRequest
{
    public int CustomerGroupId { get; set; }

    /// <summary>Phải lớn hơn 1 (<c>CK_PriceTiers_MinQty</c>).</summary>
    public int MinQuantity { get; set; }

    /// <summary>Không âm (<c>CK_PriceTiers_Price</c>).</summary>
    public decimal Price { get; set; }
}

/// <summary>Yêu cầu cập nhật bậc giá — PUT /api/v1/admin/products/{id}/price-tiers/{tierId}.</summary>
public class UpdatePriceTierRequest
{
    public int CustomerGroupId { get; set; }

    /// <summary>Phải lớn hơn 1 (<c>CK_PriceTiers_MinQty</c>).</summary>
    public int MinQuantity { get; set; }

    /// <summary>Không âm (<c>CK_PriceTiers_Price</c>).</summary>
    public decimal Price { get; set; }
}
