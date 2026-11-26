using System.Text.Json;
using SiliconHorizon.Application.Common.Models;

namespace SiliconHorizon.Application.Features.PcBuilds.Dto;

/// <summary>
/// GET /api/v1/pc-builds — tham số lọc/sắp xếp/phân trang (khớp <c>usp_PcBuild_Search</c>).
/// Kế thừa <see cref="PageRequest"/> để dùng chung quy ước <c>pageNumber</c>/<c>pageSize</c>.
/// </summary>
public class PcBuildSearchRequest : PageRequest
{
    /// <summary>Từ khoá tìm theo tên, mã bộ hoặc mô tả ngắn.</summary>
    public string? Keyword { get; set; }

    /// <summary>0 Văn phòng, 1 Gaming, 2 Đồ hoạ/Render, 3 Cao cấp; <c>null</c> = tất cả.</summary>
    public byte? Tier { get; set; }

    /// <summary>Nhóm khách hàng để áp bảng giá (giống GET /api/v1/products).</summary>
    public int? CustomerGroupId { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    /// <summary>Chỉ hiện bộ còn lắp được (đủ tồn cho mọi linh kiện trong bộ).</summary>
    public bool InStockOnly { get; set; }

    /// <summary>Chỉ hiện bộ đang ghim ở trang chủ.</summary>
    public bool FeaturedOnly { get; set; }

    /// <summary><c>order</c> (mặc định — theo SortOrder) · <c>price_asc</c> · <c>price_desc</c> · <c>name</c>.</summary>
    public string SortBy { get; set; } = "order";
}

/// <summary>
/// Một bộ PC build sẵn trong danh sách.
/// <c>partsPrice</c> = tổng giá linh kiện (đây CHÍNH LÀ giá bộ); <c>availableBuilds</c> = số bộ còn lắp được.
/// </summary>
public class PcBuildListItemDto
{
    public long Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    /// <summary>0 Văn phòng, 1 Gaming, 2 Đồ hoạ/Render, 3 Cao cấp.</summary>
    public byte Tier { get; init; }

    public string? Summary { get; init; }

    public string? ImageUrl { get; init; }

    public bool IsFeatured { get; init; }

    public int ComponentCount { get; init; }

    /// <summary>Tổng giá linh kiện theo giá hiện hành (giá bộ).</summary>
    public decimal PartsPrice { get; init; }

    /// <summary>Số bộ còn lắp được theo tồn kho hiện tại.</summary>
    public int AvailableBuilds { get; init; }

    /// <summary>Tên các linh kiện nối bằng " · " để xem nhanh trên thẻ bộ.</summary>
    public string? ComponentPreview { get; init; }
}

/// <summary>Chi tiết bộ: header + danh sách linh kiện kèm giá và tồn hiện tại.</summary>
public class PcBuildDetailDto
{
    public long Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    /// <summary>0 Văn phòng, 1 Gaming, 2 Đồ hoạ/Render, 3 Cao cấp.</summary>
    public byte Tier { get; init; }

    public string? Summary { get; init; }

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }

    public bool IsFeatured { get; init; }

    public int ComponentCount { get; init; }

    public decimal PartsPrice { get; init; }

    public int AvailableBuilds { get; init; }

    public IReadOnlyList<PcBuildComponentDto> Components { get; init; } = Array.Empty<PcBuildComponentDto>();
}

/// <summary>Một linh kiện trong bộ (kèm giá và tồn kho hiện tại để hiển thị cảnh báo).</summary>
public class PcBuildComponentDto
{
    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string CategoryCode { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    /// <summary>0 Generic, 1 CPU, 2 RAM, 3 Storage, 4 Monitor, 5 Mainboard, 6 PSU.</summary>
    public byte SpecType { get; init; }

    public string BrandName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }

    public int AvailableQty { get; init; }

    public string? ImageUrl { get; init; }

    public int WarrantyMonths { get; init; }

    public bool RequiresSerial { get; init; }

    /// <summary>0 Draft, 1 Active, 2 Discontinued.</summary>
    public byte Status { get; init; }

    public string? Note { get; init; }

    public int SortOrder { get; init; }
}

/// <summary>POST /api/v1/pc-builds/{id}/add-to-cart — số bộ cần thêm (mặc định 1).</summary>
public class AddPcBuildToCartRequest
{
    public int Quantity { get; init; } = 1;
}

/// <summary>Kết quả thêm bộ vào giỏ: từng linh kiện đã thêm và số lượng hiện có trong giỏ.</summary>
public class AddPcBuildToCartResultDto
{
    public long PcBuildId { get; init; }

    /// <summary>Số bộ vừa thêm.</summary>
    public int Quantity { get; init; }

    /// <summary>Tổng số dòng linh kiện đã thêm/cập nhật trong giỏ.</summary>
    public int ItemCount { get; init; }

    public IReadOnlyList<AddedPcBuildComponentDto> Items { get; init; } = Array.Empty<AddedPcBuildComponentDto>();
}

/// <summary>Một dòng linh kiện vừa được thêm vào giỏ theo bộ.</summary>
public class AddedPcBuildComponentDto
{
    public long ProductId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Số lượng vừa thêm (số linh kiện trong bộ × số bộ).</summary>
    public int AddedQuantity { get; init; }

    /// <summary>Số lượng HIỆN CÓ trong giỏ sau khi thêm.</summary>
    public int CartQuantity { get; init; }
}

/// <summary>
/// GET /api/v1/pc-builds/builder/catalog — danh mục linh kiện cho tab "TỰ BUILD PC".
/// 6 nhóm theo ĐÚNG THỨ TỰ CHỌN của khách, kèm thông số để giao diện chỉ gợi ý linh kiện phù hợp.
/// </summary>
public class PcBuilderCatalogDto
{
    /// <summary>6 nhóm: bo mạch chủ · CPU · RAM · ổ cứng · nguồn · màn hình (theo <c>stepOrder</c>).</summary>
    public IReadOnlyList<PcBuilderCategoryDto> Categories { get; init; } = Array.Empty<PcBuilderCategoryDto>();
}

/// <summary>Một nhóm linh kiện trong danh mục tự build.</summary>
public class PcBuilderCategoryDto
{
    /// <summary>Thứ tự chọn: 1 bo mạch chủ · 2 CPU · 3 RAM · 4 ổ cứng · 5 nguồn · 6 màn hình.</summary>
    public int StepOrder { get; init; }

    public int CategoryId { get; init; }

    public string CategoryCode { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    /// <summary>0 Generic, 1 CPU, 2 RAM, 3 Storage, 4 Monitor, 5 Mainboard, 6 PSU.</summary>
    public byte SpecType { get; init; }

    public IReadOnlyList<PcBuilderProductDto> Products { get; init; } = Array.Empty<PcBuilderProductDto>();
}

/// <summary>Một linh kiện khách có thể chọn trong tab "Tự build PC".</summary>
public class PcBuilderProductDto
{
    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string BrandName { get; init; } = string.Empty;

    public decimal BasePrice { get; init; }

    public decimal Price { get; init; }

    public int AvailableQty { get; init; }

    public string? ImageUrl { get; init; }

    public int WarrantyMonths { get; init; }

    public bool RequiresSerial { get; init; }

    /// <summary>
    /// Thông số dùng cho việc gợi ý tương thích — khoá KHÁC NHAU theo nhóm:
    /// MAINBOARD (socket · memoryType · ramSlots · m2Slots · sataPorts · chipset · formFactor),
    /// CPU (socket · tdpW · cores · hasIntegratedGpu), RAM (memoryType · modulesPerKit · capacityGbPerKit ·
    /// busMhz · formFactor), STORAGE (storageType · interface · formFactor · capacityGb),
    /// PSU (powerW · efficiencyRating · modularType), MONITOR (sizeInch · resolution · refreshRateHz · panelType).
    /// </summary>
    public JsonElement? Specs { get; init; }
}

