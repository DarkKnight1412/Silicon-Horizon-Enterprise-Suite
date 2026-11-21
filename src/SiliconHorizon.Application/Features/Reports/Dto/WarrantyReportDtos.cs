namespace SiliconHorizon.Application.Features.Reports.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO báo cáo bảo hành — khớp CHÍNH XÁC 3 result set của dbo.usp_Report_Warranty
// (Database/06_sp_hr_audit_reports.sql; Database/README.md mục 9, WAR).
//
// Proc nhận @FromDate/@ToDate (BẮT BUỘC, giờ Việt Nam) tính theo WarrantyTickets.ReceivedAt
// và @BranchId tùy chọn. Thời gian xử lý trung bình chỉ tính phiếu đã trả khách (ReturnedAt IS NOT NULL).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Số phiếu bảo hành theo trạng thái — result set 1 của <c>dbo.usp_Report_Warranty</c> (Status, Tickets).</summary>
public sealed class WarrantyStatusCountRow
{
    /// <summary>WarrantyTickets.Status: 0 Received … 6 ReturnedToCustomer.</summary>
    public byte Status { get; set; }

    public int Tickets { get; set; }
}

/// <summary>Một dòng số phiếu theo trạng thái trả ra API.</summary>
public class WarrantyStatusCountDto
{
    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public int Tickets { get; init; }

    public static WarrantyStatusCountDto FromRow(WarrantyStatusCountRow row) => new()
    {
        Status = row.Status,
        StatusName = ReportStatusText.WarrantyTicketStatusText(row.Status),
        Tickets = row.Tickets
    };
}

/// <summary>
/// Sản phẩm có nhiều phiếu bảo hành nhất (top 20) — result set 2 của <c>dbo.usp_Report_Warranty</c>
/// (ProductId, Sku, Name, Tickets, SoldQuantity).
/// </summary>
public sealed class WarrantyTopProductRow
{
    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    /// <summary>Tên sản phẩm — cột <c>Name</c> trong result set (<c>Products.Name</c>).</summary>
    public string Name { get; set; } = string.Empty;

    public int Tickets { get; set; }

    /// <summary>Số lượng đã bán của sản phẩm (đơn ở trạng thái 7 Completed / 9 PartiallyReturned / 10 Returned)
    /// — dùng để tính tỷ lệ lỗi.</summary>
    public int SoldQuantity { get; set; }
}

/// <summary>Một dòng sản phẩm lỗi nhiều trả ra API.</summary>
public class WarrantyTopProductDto
{
    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public int Tickets { get; init; }

    public int SoldQuantity { get; init; }

    public static WarrantyTopProductDto FromRow(WarrantyTopProductRow row) => new()
    {
        ProductId = row.ProductId,
        Sku = row.Sku,
        ProductName = row.Name,
        Tickets = row.Tickets,
        SoldQuantity = row.SoldQuantity
    };
}

/// <summary>
/// Thời gian xử lý trung bình — result set 3 của <c>dbo.usp_Report_Warranty</c> (AvgHandlingDays).
/// </summary>
public sealed class WarrantyHandlingRow
{
    /// <summary>Số ngày trung bình từ lúc tiếp nhận đến lúc trả khách; <c>null</c> khi chưa có phiếu nào hoàn tất.</summary>
    public decimal? AvgHandlingDays { get; set; }
}

/// <summary>Kết quả GET /api/v1/reports/warranty.</summary>
public class WarrantyReportDto
{
    public DateOnly FromDate { get; init; }

    public DateOnly ToDate { get; init; }

    /// <summary>Chi nhánh đã áp dụng (<c>null</c> = toàn hệ thống).</summary>
    public int? BranchId { get; init; }

    /// <summary>Số phiếu theo trạng thái.</summary>
    public IReadOnlyList<WarrantyStatusCountDto> Statuses { get; init; } = Array.Empty<WarrantyStatusCountDto>();

    /// <summary>Top 20 sản phẩm có nhiều phiếu bảo hành nhất.</summary>
    public IReadOnlyList<WarrantyTopProductDto> TopDefectiveProducts { get; init; } = Array.Empty<WarrantyTopProductDto>();

    /// <summary>Thời gian xử lý trung bình (ngày) — <c>null</c> khi chưa có phiếu nào hoàn tất trong kỳ.</summary>
    public decimal? AvgHandlingDays { get; init; }
}

/// <summary>
/// Tham số GET /api/v1/reports/warranty?from=&amp;to=&amp;branchId=.
/// </summary>
/// <remarks>
/// <b>from/to là bắt buộc</b> (<c>@FromDate</c>/<c>@ToDate</c> của proc không có giá trị mặc định),
/// tính theo ngày NHẬN phiếu (WarrantyTickets.ReceivedAt).
/// <see cref="BranchId"/> chỉ có hiệu lực với Admin.
/// </remarks>
public class WarrantyReportQuery
{
    /// <summary>Ngày bắt đầu (bao gồm), theo giờ Việt Nam — bắt buộc.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Ngày kết thúc (bao gồm), theo giờ Việt Nam — bắt buộc.</summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>Chỉ Admin: lọc theo chi nhánh (bỏ trống = toàn hệ thống).</summary>
    public int? BranchId { get; set; }
}