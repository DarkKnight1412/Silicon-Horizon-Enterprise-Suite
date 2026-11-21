namespace SiliconHorizon.Application.Features.Reports.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO báo cáo doanh thu — khớp CHÍNH XÁC các result set của dbo.usp_Report_Revenue
// (Database/06_sp_hr_audit_reports.sql; Database/README.md mục 9, BRD mục 11).
//
// Proc có 2 nhóm kết quả tùy @GroupBy:
//   • 'category'      → 1 result set: CategoryId, CategoryName, Quantity, Revenue
//   • day|month|branch → 1 result set: GroupKey, Orders, GrossRevenue, Refunds, NetRevenue, Cost, GrossProfit
//
// Doanh thu ghi nhận theo ngày HOÀN TẤT đơn (PAY-05), không tính phí vận chuyển; lợi nhuận gộp =
// (doanh thu thuần − hoàn tiền) − giá vốn (serial lấy UnitCost, hàng không serial lấy giá nhập gần nhất).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Một dòng doanh thu gộp theo ngày / tháng / chi nhánh — result set của <c>dbo.usp_Report_Revenue</c>
/// khi <c>@GroupBy</c> khác <c>'category'</c>.
/// </summary>
public sealed class RevenueGroupRow
{
    /// <summary>
    /// Khóa nhóm: <c>yyyy-MM-dd</c> (day), <c>yyyy-MM</c> (month) hoặc <c>BranchId</c> dạng chuỗi (branch).
    /// </summary>
    public string GroupKey { get; set; } = string.Empty;

    public int Orders { get; set; }

    /// <summary>Tổng doanh thu trước khi trừ hoàn tiền (SubTotal − DiscountTotal).</summary>
    public decimal GrossRevenue { get; set; }

    /// <summary>Tổng tiền đã hoàn cho khách (ReturnRequests ở trạng thái Refunded/Closed).</summary>
    public decimal Refunds { get; set; }

    /// <summary>Doanh thu thuần = GrossRevenue − Refunds.</summary>
    public decimal NetRevenue { get; set; }

    /// <summary>Giá vốn hàng bán (ước tính với hàng không quản lý serial).</summary>
    public decimal Cost { get; set; }

    /// <summary>Lợi nhuận gộp = NetRevenue − Cost.</summary>
    public decimal GrossProfit { get; set; }
}

/// <summary>Một dòng doanh thu trả ra API.</summary>
public class RevenueGroupDto
{
    public string GroupKey { get; init; } = string.Empty;

    public int Orders { get; init; }

    public decimal GrossRevenue { get; init; }

    public decimal Refunds { get; init; }

    public decimal NetRevenue { get; init; }

    public decimal Cost { get; init; }

    public decimal GrossProfit { get; init; }

    public static RevenueGroupDto FromRow(RevenueGroupRow row) => new()
    {
        GroupKey = row.GroupKey,
        Orders = row.Orders,
        GrossRevenue = row.GrossRevenue,
        Refunds = row.Refunds,
        NetRevenue = row.NetRevenue,
        Cost = row.Cost,
        GrossProfit = row.GrossProfit
    };
}

/// <summary>
/// Một dòng doanh thu theo danh mục — result set của <c>dbo.usp_Report_Revenue</c> khi <c>@GroupBy = 'category'</c>.
/// </summary>
public sealed class RevenueCategoryRow
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Số lượng sản phẩm đã bán.</summary>
    public int Quantity { get; set; }

    /// <summary>Doanh thu của danh mục (chưa trừ hoàn tiền và giá vốn).</summary>
    public decimal Revenue { get; set; }
}

/// <summary>Một dòng doanh thu theo danh mục trả ra API.</summary>
public class RevenueCategoryDto
{
    public int CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal Revenue { get; init; }

    public static RevenueCategoryDto FromRow(RevenueCategoryRow row) => new()
    {
        CategoryId = row.CategoryId,
        CategoryName = row.CategoryName,
        Quantity = row.Quantity,
        Revenue = row.Revenue
    };
}

/// <summary>
/// Kết quả GET /api/v1/reports/revenue — chỉ một trong hai danh sách có dữ liệu, tùy <see cref="GroupBy"/>.
/// </summary>
public class RevenueReportDto
{
    /// <summary>day | month | branch | category.</summary>
    public string GroupBy { get; init; } = string.Empty;

    public DateOnly FromDate { get; init; }

    public DateOnly ToDate { get; init; }

    /// <summary>Chi nhánh đã áp dụng (<c>null</c> = toàn hệ thống) — Admin không truyền <c>branchId</c> thì null.</summary>
    public int? BranchId { get; init; }

    /// <summary>Doanh thu gộp theo ngày/tháng/chi nhánh (rỗng khi <c>groupBy = category</c>).</summary>
    public IReadOnlyList<RevenueGroupDto> Groups { get; init; } = Array.Empty<RevenueGroupDto>();

    /// <summary>Doanh thu theo danh mục (rỗng khi <c>groupBy</c> khác <c>category</c>).</summary>
    public IReadOnlyList<RevenueCategoryDto> Categories { get; init; } = Array.Empty<RevenueCategoryDto>();
}

/// <summary>
/// Tham số GET /api/v1/reports/revenue?from=&amp;to=&amp;groupBy=&amp;branchId= (BRD mục 11).
/// </summary>
/// <remarks>
/// <b>from/to là bắt buộc</b> (<c>@FromDate</c>/<c>@ToDate</c> của proc không có giá trị mặc định).
/// <see cref="BranchId"/> chỉ có hiệu lực với Admin; Quản lý chi nhánh luôn bị ép về chi nhánh trong token.
/// </remarks>
public class RevenueReportQuery
{
    /// <summary>Ngày bắt đầu (bao gồm), theo giờ Việt Nam — bắt buộc.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Ngày kết thúc (bao gồm), theo giờ Việt Nam — bắt buộc.</summary>
    public DateOnly? ToDate { get; set; }

    /// <summary><c>day</c> (mặc định) | <c>month</c> | <c>branch</c> | <c>category</c>. Giá trị khác → 400.</summary>
    public string? GroupBy { get; set; }

    /// <summary>Chỉ Admin: lọc theo chi nhánh (bỏ trống = toàn hệ thống).</summary>
    public int? BranchId { get; set; }
}
