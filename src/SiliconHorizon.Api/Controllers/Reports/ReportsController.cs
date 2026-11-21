using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Reports;
using SiliconHorizon.Application.Features.Reports.Dto;

namespace SiliconHorizon.Api.Controllers.Reports;

/// <summary>
/// Báo cáo — GET /api/v1/reports/revenue | inventory | serial-status | debt | warranty
/// (Database/README.md mục 9, BRD mục 11).
/// </summary>
/// <remarks>
/// API ép phạm vi chi nhánh từ token (BRD 2.2): Quản lý chi nhánh và thủ kho chỉ xem số liệu của chi nhánh
/// mình, Admin xem toàn hệ thống và có thể lọc theo <c>branchId</c>. Riêng báo cáo công nợ chỉ dành cho
/// Admin vì <c>usp_Report_Debt</c> không có tham số chi nhánh.
/// </remarks>
[ApiController]
[Route("api/v1/reports")]
[Authorize]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService) => _reportService = reportService;

    /// <summary>
    /// GET /api/v1/reports/revenue?from=&amp;to=&amp;groupBy=day|month|branch|category&amp;branchId=
    /// — doanh thu, hoàn tiền, giá vốn, lợi nhuận gộp (usp_Report_Revenue).
    /// </summary>
    /// <remarks>
    /// <c>from</c>/<c>to</c> bắt buộc, theo giờ Việt Nam và bao gồm cả ngày <c>to</c>. Doanh thu ghi nhận theo
    /// ngày hoàn tất đơn và không tính phí vận chuyển. Quyền: QL (chi nhánh mình), AD.
    /// </remarks>
    [HttpGet("revenue")]
    [Authorize(Roles = ReportRoles.Approvers)]
    [ProducesResponseType(typeof(RevenueReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RevenueReportDto>> GetRevenue(
        [FromQuery] RevenueReportQuery query,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetRevenueAsync(query, cancellationToken));

    /// <summary>
    /// GET /api/v1/reports/inventory?slowDays=&amp;branchId= — giá trị tồn theo chi nhánh/danh mục và hàng
    /// tồn lâu không xuất (usp_Report_Inventory).
    /// </summary>
    /// <remarks>
    /// Không có tham số ngày (số liệu hiện thời). <c>slowDays</c> mặc định 90 ngày — hàng còn tồn mà không có
    /// bút toán xuất kho nào trong khoảng đó. Quyền: TK, QL, AD.
    /// </remarks>
    [HttpGet("inventory")]
    [Authorize(Roles = ReportRoles.StockReports)]
    [ProducesResponseType(typeof(InventoryReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InventoryReportDto>> GetInventory(
        [FromQuery] InventoryReportQuery query,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetInventoryAsync(query, cancellationToken));

    /// <summary>
    /// GET /api/v1/reports/serial-status?productId=&amp;branchId= — số lượng serial theo trạng thái
    /// (usp_Report_SerialStatus).
    /// </summary>
    /// <remarks>
    /// Chỉ trả các trạng thái đang có serial (0 Trong kho … 9 Đã hủy) — trạng thái không có serial nào sẽ
    /// vắng mặt trong kết quả. Quyền: TK, QL, AD.
    /// </remarks>
    [HttpGet("serial-status")]
    [Authorize(Roles = ReportRoles.StockReports)]
    [ProducesResponseType(typeof(IReadOnlyList<SerialStatusCountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SerialStatusCountDto>>> GetSerialStatus(
        [FromQuery] SerialStatusReportQuery query,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetSerialStatusAsync(query, cancellationToken));

    /// <summary>
    /// GET /api/v1/reports/debt — công nợ phải thu (khách) và phải trả (nhà cung cấp) kèm phần quá hạn
    /// (usp_Report_Debt).
    /// </summary>
    /// <remarks>
    /// Không có tham số: proc trả toàn bộ đối tượng còn số dư ≠ 0 và không lọc được theo chi nhánh, nên
    /// endpoint chỉ dành cho Admin. Quyền: AD.
    /// </remarks>
    [HttpGet("debt")]
    [Authorize(Roles = ReportRoles.AdminOnly)]
    [ProducesResponseType(typeof(DebtReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DebtReportDto>> GetDebt(CancellationToken cancellationToken)
        => Ok(await _reportService.GetDebtAsync(cancellationToken));

    /// <summary>
    /// GET /api/v1/reports/warranty?from=&amp;to=&amp;branchId= — số phiếu bảo hành theo trạng thái, top 20
    /// sản phẩm lỗi nhiều và thời gian xử lý trung bình (usp_Report_Warranty).
    /// </summary>
    /// <remarks>
    /// <c>from</c>/<c>to</c> bắt buộc, tính theo ngày NHẬN phiếu và bao gồm cả ngày <c>to</c>. Thời gian xử lý
    /// trung bình chỉ tính các phiếu đã trả khách (<c>null</c> khi chưa có phiếu nào hoàn tất trong kỳ).
    /// Quyền: QL (chi nhánh mình), AD.
    /// </remarks>
    [HttpGet("warranty")]
    [Authorize(Roles = ReportRoles.Approvers)]
    [ProducesResponseType(typeof(WarrantyReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WarrantyReportDto>> GetWarranty(
        [FromQuery] WarrantyReportQuery query,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetWarrantyAsync(query, cancellationToken));
}
