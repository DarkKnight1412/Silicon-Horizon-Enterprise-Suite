using System.Text.Json;
using SiliconHorizon.Application.Common.Models;

namespace SiliconHorizon.Application.Features.Catalog.Dto;

/// <summary>Tham số GET /api/products — khớp đúng tham số <c>usp_Product_Search</c>.</summary>
public class ProductSearchRequest : PageRequest
{
    /// <summary>Tìm trong tên sản phẩm và đầu mã SKU (proc dùng LIKE).</summary>
    public string? Keyword { get; set; }

    /// <summary>Mã danh mục (CPU, RAM, ...) — proc tự mở rộng sang danh mục con.</summary>
    public string? CategoryCode { get; set; }

    public int? BrandId { get; set; }

    /// <summary>Nhóm khách để lấy giá; bỏ trống → dùng nhóm mặc định (khách lẻ).</summary>
    public int? CustomerGroupId { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public bool InStockOnly { get; set; }

    public bool OnPromotion { get; set; }

    /// <summary>
    /// Lọc theo thông số kỹ thuật, dạng rút gọn như tài liệu: <c>specFilters=memoryType:DDR5,capacityGbPerKit:32</c>.
    /// API tự chuyển thành <c>@SpecFiltersJson</c> cho proc.
    /// </summary>
    public string? SpecFilters { get; set; }

    /// <summary>newest | price_asc | price_desc | name | discount</summary>
    public string SortBy { get; set; } = "newest";

    /// <summary>Tách chuỗi <see cref="SpecFilters"/> thành danh sách điều kiện (bỏ qua phần tử sai định dạng).</summary>
    public IReadOnlyList<ProductSpecFilter> ParseSpecFilters()
    {
        if (string.IsNullOrWhiteSpace(SpecFilters))
        {
            return Array.Empty<ProductSpecFilter>();
        }

        return SpecFilters
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
            .Select(parts => new ProductSpecFilter(parts[0], parts[1]))
            .ToList();
    }
}

/// <summary>Một điều kiện lọc theo thông số — proc nhận qua <c>@SpecFiltersJson</c> dạng camelCase.</summary>
public record ProductSpecFilter(string Field, string Value);

/// <summary>Một dòng trong kết quả tìm kiếm (<c>usp_Product_Search</c>).</summary>
public class ProductListItemDto
{
    public long Id { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public int BrandId { get; init; }

    public string BrandName { get; init; } = string.Empty;

    public int CategoryId { get; init; }

    public bool RequiresSerial { get; init; }

    public int WarrantyMonths { get; init; }

    /// <summary>Giá niêm yết (BasePrice).</summary>
    public decimal BasePrice { get; init; }

    /// <summary>Giá sau khuyến mãi/bảng giá theo nhóm khách (FinalUnitPrice).</summary>
    public decimal Price { get; init; }

    public long? PromotionId { get; init; }

    public int AvailableQty { get; init; }

    public string? ImageUrl { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>Ảnh khi tạo/sửa sản phẩm — serialize thành <c>@ImagesJson</c> (camelCase: url, sortOrder, isPrimary).</summary>
public record ProductImageInput(string Url, int? SortOrder, bool? IsPrimary);

/// <summary>POST /api/admin/products — khớp <c>usp_Product_Create</c>.</summary>
public class CreateProductRequest
{
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public int BrandId { get; set; }

    public int CategoryId { get; set; }

    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    public decimal ListPrice { get; set; }

    public decimal? LastImportPrice { get; set; }

    public bool RequiresSerial { get; set; } = true;

    public int MinStockDefault { get; set; }

    public int WarrantyMonths { get; set; }

    public string? WarrantyTerms { get; set; }

    /// <summary>0 Draft, 1 Active, 2 Discontinued.</summary>
    public byte Status { get; set; }

    /// <summary>Thông số theo loại danh mục — chuyển nguyên văn vào <c>@SpecsJson</c> (camelCase).</summary>
    public JsonElement? Specs { get; set; }

    public List<ProductImageInput>? Images { get; set; }
}

/// <summary>PUT /api/admin/products/{id} — khớp <c>usp_Product_Update</c> (không đổi Category).</summary>
public class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public int BrandId { get; set; }

    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    public decimal ListPrice { get; set; }

    public bool RequiresSerial { get; set; }

    public int MinStockDefault { get; set; }

    public int WarrantyMonths { get; set; }

    public string? WarrantyTerms { get; set; }

    public JsonElement? Specs { get; set; }

    public List<ProductImageInput>? Images { get; set; }
}

/// <summary>PATCH /api/admin/products/{id}/status.</summary>
public record SetProductStatusRequest(byte Status);

/// <summary>PUT /api/admin/products/{id}/prices/{groupId} — khớp <c>usp_ProductPrice_Upsert</c>.</summary>
public record UpsertProductPriceRequest(decimal Price, long? ApprovedBy);
