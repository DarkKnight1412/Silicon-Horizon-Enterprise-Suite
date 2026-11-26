namespace SiliconHorizon.Application.Features.PcBuilds.Dto;

/// <summary>
/// Lớp ánh xạ cột result set của proc (tên property PHẢI khớp tên cột).
/// Dùng property <c>get; set;</c> thường (không phải <c>init</c>) để Dapper gán được.
/// </summary>
public abstract class PcBuildHeaderRowShape
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    /// <summary>0 Văn phòng, 1 Gaming, 2 Đồ hoạ/Render, 3 Cao cấp.</summary>
    public byte Tier { get; set; }

    public string? Summary { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsFeatured { get; set; }

    public int ComponentCount { get; set; }

    /// <summary>Tổng giá linh kiện trong bộ (theo giá hiện hành) — chính là giá bộ.</summary>
    public decimal PartsPrice { get; set; }

    /// <summary>Số bộ còn lắp được (tồn khả dụng nhỏ nhất của các linh kiện).</summary>
    public int AvailableBuilds { get; set; }
}

/// <summary>Result set của <c>usp_PcBuild_Search</c> — thêm cột <c>TotalCount</c> (COUNT(*) OVER ()).</summary>
public class PcBuildSearchRow : PcBuildHeaderRowShape
{
    /// <summary>Dòng xem nhanh tên các linh kiện: "CPU · RAM · SSD...".</summary>
    public string? ComponentPreview { get; set; }

    public int TotalCount { get; set; }
}

/// <summary>Result set 1 của <c>usp_PcBuild_GetById</c> (header bộ).</summary>
public class PcBuildDetailRow : PcBuildHeaderRowShape
{
    public string? Description { get; set; }
}

/// <summary>Result set 2 của <c>usp_PcBuild_GetById</c> (linh kiện trong bộ).</summary>
public class PcBuildComponentRow
{
    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string CategoryCode { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    /// <summary>0 Generic, 1 CPU, 2 RAM, 3 Storage, 4 Monitor, 5 Mainboard, 6 PSU.</summary>
    public byte SpecType { get; set; }

    public string BrandName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public int AvailableQty { get; set; }

    public string? ImageUrl { get; set; }

    public int WarrantyMonths { get; set; }

    public bool RequiresSerial { get; set; }

    /// <summary>0 Draft, 1 Active, 2 Discontinued.</summary>
    public byte Status { get; set; }

    public string? Note { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>Result set của <c>usp_PcBuild_AddToCart</c> (từng linh kiện đã thêm vào giỏ).</summary>
public class PcBuildCartItemRow
{
    public long ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Số lượng vừa thêm (số linh kiện × số bộ).</summary>
    public int AddedQuantity { get; set; }

    /// <summary>Số lượng HIỆN CÓ trong giỏ sau khi thêm.</summary>
    public int CartQuantity { get; set; }
}

/// <summary>
/// Result set của <c>usp_PcBuild_BuilderCatalog</c> — một linh kiện trong danh mục tự build
/// (một result set phẳng cho cả 6 nhóm; service nhóm lại theo <c>StepOrder</c> + <c>CategoryId</c>).
/// </summary>
public class PcBuilderCatalogRow
{
    public int StepOrder { get; set; }

    public int CategoryId { get; set; }

    public string CategoryCode { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public byte SpecType { get; set; }

    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string BrandName { get; set; } = string.Empty;

    public decimal BasePrice { get; set; }

    public decimal Price { get; set; }

    public int AvailableQty { get; set; }

    public string? ImageUrl { get; set; }

    public int WarrantyMonths { get; set; }

    public bool RequiresSerial { get; set; }

    /// <summary>Thông số dạng JSON (camelCase) — khoá khác nhau theo nhóm linh kiện.</summary>
    public string? SpecsJson { get; set; }
}

