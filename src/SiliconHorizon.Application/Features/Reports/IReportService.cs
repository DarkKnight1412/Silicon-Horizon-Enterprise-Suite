using SiliconHorizon.Application.Features.Reports.Dto;

namespace SiliconHorizon.Application.Features.Reports;

/// <summary>
/// Use case Báo cáo (BRD mục 11, Database/README.md mục 9). Mọi phương thức chỉ gọi stored procedure
/// trong <c>Database/06_sp_hr_audit_reports.sql</c> (nhóm <c>usp_Report_*</c>) và trả dữ liệu đã tổng hợp.
/// </summary>
/// <remarks>
/// Tham số ngày (<c>from</c>/<c>to</c>) là ngày theo giờ Việt Nam; proc tự đổi sang UTC khi so với
/// <c>CompletedAt</c>/<c>ReceivedAt</c> vì dữ liệu lưu UTC.
/// </remarks>
public interface IReportService
{
    /// <summary>
    /// Doanh thu, hoàn tiền, giá vốn, lợi nhuận gộp theo ngày/tháng/chi nhánh/danh mục —
    /// GET /api/v1/reports/revenue (usp_Report_Revenue). Quyền: QL (chi nhánh mình), AD.
    /// </summary>
    Task<RevenueReportDto> GetRevenueAsync(
        RevenueReportQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Giá trị tồn theo chi nhánh/danh mục và danh sách hàng tồn lâu không xuất —
    /// GET /api/v1/reports/inventory (usp_Report_Inventory). Quyền: TK, QL, AD.
    /// </summary>
    Task<InventoryReportDto> GetInventoryAsync(
        InventoryReportQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Số lượng serial theo trạng thái — GET /api/v1/reports/serial-status (usp_Report_SerialStatus).
    /// Quyền: TK, QL, AD.
    /// </summary>
    Task<IReadOnlyList<SerialStatusCountDto>> GetSerialStatusAsync(
        SerialStatusReportQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Công nợ phải thu (khách) và phải trả (nhà cung cấp) kèm phần quá hạn —
    /// GET /api/v1/reports/debt (usp_Report_Debt). Quyền: AD (proc không lọc được theo chi nhánh).
    /// </summary>
    Task<DebtReportDto> GetDebtAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Phiếu bảo hành theo trạng thái, sản phẩm lỗi nhiều, thời gian xử lý trung bình —
    /// GET /api/v1/reports/warranty (usp_Report_Warranty). Quyền: QL, AD.
    /// </summary>
    Task<WarrantyReportDto> GetWarrantyAsync(
        WarrantyReportQuery query,
        CancellationToken cancellationToken = default);
}
