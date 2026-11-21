namespace SiliconHorizon.Application.Features.Reports.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO báo cáo kho — khớp CHÍNH XÁC 2 result set của dbo.usp_Report_Inventory
// (Database/06_sp_hr_audit_reports.sql; Database/README.md mục 9).
//
// Proc KHÔNG nhận from/to date: giá trị tồn là số liệu hiện thời, còn "hàng tồn lâu không xuất" tính theo
// @SlowDays (mặc định 90 ngày) so với lần xuất kho (StockMovements.MovementType = 3) gần nhất.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Giá trị tồn theo chi nhánh + danh mục — result set 1 của <c>dbo.usp_Report_Inventory</c>
/// (BranchId, BranchName, CategoryId, CategoryName, PhysicalQty, ReservedQty, AvailableQty, StockValue).
/// </summary>
public sealed class InventoryValueRow
{
    public int BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public int PhysicalQty { get; set; }

    public int ReservedQty { get; set; }

    public int AvailableQty { get; set; }

    /// <summary>Giá trị tồn: hàng quản lý serial lấy tổng <c>SerialItems.UnitCost</c>, còn lại lấy giá nhập gần nhất.</summary>
    public decimal StockValue { get; set; }
}

/// <summary>Một dòng giá trị tồn trả ra API.</summary>
public class InventoryValueDto
{
    public int BranchId { get; init; }

    public string BranchName { get; init; } = string.Empty;

    public int CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public int PhysicalQty { get; init; }

    public int ReservedQty { get; init; }

    public int AvailableQty { get; init; }

    public decimal StockValue { get; init; }

    public static InventoryValueDto FromRow(InventoryValueRow row) => new()
    {
        BranchId = row.BranchId,
        BranchName = row.BranchName,
        CategoryId = row.CategoryId,
        CategoryName = row.CategoryName,
        PhysicalQty = row.PhysicalQty,
        ReservedQty = row.ReservedQty,
        AvailableQty = row.AvailableQty,
        StockValue = row.StockValue
    };
}

/// <summary>
/// Hàng tồn lâu không xuất — result set 2 của <c>dbo.usp_Report_Inventory</c>
/// (BranchId, ProductId, Sku, Name, PhysicalQty, LastIssueAt).
/// </summary>
public sealed class SlowMovingStockRow
{
    public int BranchId { get; set; }

    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    /// <summary>Tên sản phẩm — cột <c>Name</c> trong result set (<c>Products.Name</c>).</summary>
    public string Name { get; set; } = string.Empty;

    public int PhysicalQty { get; set; }

    /// <summary>Lần xuất kho gần nhất — NULL nghĩa là chưa từng xuất.</summary>
    public DateTime? LastIssueAt { get; set; }
}

/// <summary>Một dòng hàng tồn lâu không xuất trả ra API.</summary>
public class SlowMovingStockDto
{
    public int BranchId { get; init; }

    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public int PhysicalQty { get; init; }

    /// <summary>UTC; <c>null</c> = chưa từng xuất kho.</summary>
    public DateTime? LastIssueAt { get; init; }

    public static SlowMovingStockDto FromRow(SlowMovingStockRow row) => new()
    {
        BranchId = row.BranchId,
        ProductId = row.ProductId,
        Sku = row.Sku,
        ProductName = row.Name,
        PhysicalQty = row.PhysicalQty,
        LastIssueAt = row.LastIssueAt
    };
}

/// <summary>Kết quả GET /api/v1/reports/inventory.</summary>
public class InventoryReportDto
{
    /// <summary>Chi nhánh đã áp dụng (<c>null</c> = toàn hệ thống).</summary>
    public int? BranchId { get; init; }

    /// <summary>Số ngày không xuất kho được coi là tồn lâu (mặc định 90 theo proc).</summary>
    public int SlowDays { get; init; }

    public IReadOnlyList<InventoryValueDto> Values { get; init; } = Array.Empty<InventoryValueDto>();

    public IReadOnlyList<SlowMovingStockDto> SlowMoving { get; init; } = Array.Empty<SlowMovingStockDto>();
}

/// <summary>
/// Tham số GET /api/v1/reports/inventory?slowDays=&amp;branchId=.
/// </summary>
/// <remarks>
/// Báo cáo này không có tham số ngày: giá trị tồn là số liệu hiện thời.
/// <see cref="BranchId"/> chỉ có hiệu lực với Admin (Quản lý/thủ kho bị ép về chi nhánh trong token).
/// </remarks>
public class InventoryReportQuery
{
    /// <summary>Số ngày tính "tồn lâu không xuất" — mặc định 90 (giá trị của proc).</summary>
    public int? SlowDays { get; set; }

    /// <summary>Chỉ Admin: lọc theo chi nhánh (bỏ trống = toàn hệ thống).</summary>
    public int? BranchId { get; set; }
}
