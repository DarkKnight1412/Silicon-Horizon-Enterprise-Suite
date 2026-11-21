namespace SiliconHorizon.Application.Features.Catalog.Dto;

/// <summary>
/// Lớp ánh xạ cột result set của proc (tên property PHẢI khớp tên cột).
/// Dùng property <c>get; set;</c> thường (không phải <c>init</c>) để Dapper gán được.
/// </summary>
/// <remarks>Result set của <c>usp_Product_Search</c> — thêm cột <c>TotalCount</c> (COUNT(*) OVER ()).</remarks>
public class ProductSearchRow : ProductListItemDtoShape
{
    public int TotalCount { get; set; }
}

/// <summary>Các cột chung của <c>usp_Product_Search</c> và header <c>usp_Product_GetById</c>.</summary>
public abstract class ProductListItemDtoShape
{
    public long Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public int BrandId { get; set; }

    public string BrandName { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public bool RequiresSerial { get; set; }

    public int WarrantyMonths { get; set; }

    public decimal BasePrice { get; set; }

    public decimal Price { get; set; }

    public long? PromotionId { get; set; }

    public int AvailableQty { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Result set 1 của <c>usp_Product_GetById</c> (header).</summary>
public class ProductDetailRow : ProductListItemDtoShape
{
    public string CategoryCode { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public byte SpecType { get; set; }

    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    public string? WarrantyTerms { get; set; }

    public byte Status { get; set; }

    public decimal ListPrice { get; set; }
}

/// <summary>Result set 2 của <c>usp_Product_GetById</c> (ảnh).</summary>
public class ProductImageRow
{
    public long Id { get; set; }

    public string Url { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }
}

/// <summary>Result set cuối của <c>usp_Product_GetById</c> (tồn theo chi nhánh).</summary>
public class ProductStockByBranchRow
{
    public int BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public int AvailableQty { get; set; }
}

/// <summary>Result set của <c>usp_Compat_Check</c>.</summary>
public class CompatibilityWarningRow
{
    public byte Severity { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

/// <summary>Result set của <c>usp_Product_Create</c> (một cột ProductId).</summary>
public class ProductIdRow
{
    public long ProductId { get; set; }
}
