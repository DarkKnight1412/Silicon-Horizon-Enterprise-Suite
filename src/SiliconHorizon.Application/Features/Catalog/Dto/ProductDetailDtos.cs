namespace SiliconHorizon.Application.Features.Catalog.Dto;

/// <summary>POST /api/compatibility/check — khớp <c>usp_Compat_Check (@ItemsJson)</c>.</summary>
public record CompatibilityCheckRequest(List<CompatibilityItem> Items);

/// <summary>Một dòng hàng cần kiểm tra tương thích.</summary>
public record CompatibilityItem(long ProductId, int Quantity);

/// <summary>Cảnh báo tương thích (result set của <c>usp_Compat_Check</c>: Severity, Code, Message).</summary>
public class CompatibilityWarningDto
{
    /// <summary>0 = Info, 1 = Warning (proc hiện chỉ phát Warning).</summary>
    public byte Severity { get; init; }

    /// <summary>Ví dụ: SOCKET_MISMATCH, MEMORY_TYPE_MISMATCH, RAM_SLOTS_EXCEEDED, PSU_POWER_LOW.</summary>
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

/// <summary>Kết quả kiểm tra tương thích. Cảnh báo KHÔNG chặn đặt hàng (PRD-10).</summary>
public class CompatibilityCheckResultDto
{
    public bool IsCompatible => Warnings.Count == 0;

    public IReadOnlyList<CompatibilityWarningDto> Warnings { get; init; } = Array.Empty<CompatibilityWarningDto>();
}

/// <summary>
/// Chi tiết sản phẩm — gom 4 result set của <c>usp_Product_GetById</c>:
/// header, ảnh, thông số (cột thay đổi theo loại danh mục), tồn theo chi nhánh.
/// </summary>
public class ProductDetailDto
{
    public long Id { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public int BrandId { get; init; }

    public string BrandName { get; init; } = string.Empty;

    public int CategoryId { get; init; }

    public string CategoryCode { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    /// <summary>0 Generic, 1 CPU, 2 RAM, 3 Storage, 4 Monitor, 5 Mainboard, 6 PSU.</summary>
    public byte SpecType { get; init; }

    public string? ShortDescription { get; init; }

    public string? Description { get; init; }

    public bool RequiresSerial { get; init; }

    public int WarrantyMonths { get; init; }

    public string? WarrantyTerms { get; init; }

    /// <summary>0 Draft, 1 Active, 2 Discontinued.</summary>
    public byte Status { get; init; }

    public decimal ListPrice { get; init; }

    public decimal BasePrice { get; init; }

    public decimal Price { get; init; }

    public long? PromotionId { get; init; }

    public int AvailableQty { get; init; }

    public IReadOnlyList<ProductImageDto> Images { get; init; } = Array.Empty<ProductImageDto>();

    /// <summary>
    /// Thông số kỹ thuật: bảng Specs_* khác cột nhau theo <see cref="SpecType"/> nên trả dạng
    /// "tên cột → giá trị". Mỗi sản phẩm có 1 dòng thông số (null nếu danh mục không có thông số riêng).
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Specifications { get; init; }

    /// <summary>Cổng kết nối của màn hình — result set riêng, chỉ có khi <see cref="SpecType"/> = 4 (Monitor).</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> MonitorPorts { get; init; }
        = Array.Empty<IReadOnlyDictionary<string, object?>>();

    public IReadOnlyList<ProductStockByBranchDto> StockByBranch { get; init; }
        = Array.Empty<ProductStockByBranchDto>();
}

/// <summary>Ảnh sản phẩm (result set 2 của <c>usp_Product_GetById</c>).</summary>
public class ProductImageDto
{
    public long Id { get; init; }

    public string Url { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public bool IsPrimary { get; init; }
}

/// <summary>Tồn khả dụng của sản phẩm tại một chi nhánh (result set cuối của <c>usp_Product_GetById</c>).</summary>
public class ProductStockByBranchDto
{
    public int BranchId { get; init; }

    public string BranchName { get; init; } = string.Empty;

    public int AvailableQty { get; init; }
}

/// <summary>Danh mục hoặc thương hiệu cho API công khai (đọc bằng EF Core — không cần proc).</summary>
public record ReferenceItemDto(int Id, string? Code, string Name);
