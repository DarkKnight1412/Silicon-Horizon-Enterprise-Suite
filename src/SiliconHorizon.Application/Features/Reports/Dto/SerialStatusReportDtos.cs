namespace SiliconHorizon.Application.Features.Reports.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO báo cáo serial theo trạng thái — khớp result set của dbo.usp_Report_SerialStatus
// (Status, Quantity) — Database/README.md mục 9.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Một dòng đếm serial theo trạng thái — result set của <c>dbo.usp_Report_SerialStatus</c>.</summary>
public sealed class SerialStatusCountRow
{
    /// <summary>SerialItems.Status: 0 InStock, 1 Allocated, 2 InTransfer, 3 Sold, 4 Rma, 5 Returned,
    /// 6 Replaced, 7 Defective, 8 ReturnedToSupplier, 9 Scrapped.</summary>
    public byte Status { get; set; }

    public int Quantity { get; set; }
}

/// <summary>Một dòng đếm serial theo trạng thái trả ra API.</summary>
public class SerialStatusCountDto
{
    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public static SerialStatusCountDto FromRow(SerialStatusCountRow row) => new()
    {
        Status = row.Status,
        StatusName = ReportStatusText.SerialStatusText(row.Status),
        Quantity = row.Quantity
    };
}

/// <summary>
/// Tham số GET /api/v1/reports/serial-status?productId=&amp;branchId= — cả hai đều tùy chọn.
/// </summary>
/// <remarks><see cref="BranchId"/> chỉ có hiệu lực với Admin; các vai trò khác bị ép về chi nhánh trong token.</remarks>
public class SerialStatusReportQuery
{
    /// <summary>Chỉ đếm serial của một sản phẩm.</summary>
    public long? ProductId { get; set; }

    /// <summary>Chỉ Admin: lọc theo chi nhánh (bỏ trống = toàn hệ thống).</summary>
    public int? BranchId { get; set; }
}
