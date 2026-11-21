using SiliconHorizon.Application.Features.Commission.Dto;

namespace SiliconHorizon.Application.Features.Commission;

/// <summary>
/// Use case hoa hồng — KPI-01..06 và Database/README.md mục 8 (UC-28).
/// Mọi phương thức chỉ gọi stored procedure trong <c>Database/05_sp_warranty_returns_commission.sql</c>.
/// </summary>
/// <remarks>
/// Luồng nghiệp vụ:
/// <list type="bullet">
/// <item>Hoa hồng phát sinh tự động khi đơn hoàn tất (<c>usp_Order_Complete</c> → <c>usp_Internal_AccrueCommission</c>),
/// chia theo tỷ lệ nhân sự tư vấn; đơn online không có nhân viên tư vấn thì không phát sinh (KPI-02/KPI-03).</item>
/// <item>Khách đổi trả thì <c>usp_Return_Refund</c> tự gọi <c>usp_Internal_ClawbackCommission</c> để ghi bút toán âm (KPI-04).</item>
/// <item>Điều chỉnh thủ công và chốt kỳ chỉ dành cho Admin; kỳ đã chốt thì bút toán mới rơi vào kỳ đang mở
/// kế tiếp (<c>usp_Internal_GetOpenPeriod</c>) để không sửa dữ liệu đã chốt (KPI-05/KPI-06).</item>
/// </list>
/// </remarks>
public interface ICommissionService
{
    /// <summary>
    /// Tổng hợp hoa hồng theo nhân viên và chi tiết từng bút toán của một tháng —
    /// GET /api/v1/commissions?yearMonth= (usp_Commission_Summary, 3 result set).
    /// Nhân viên chỉ xem của mình (ép <c>@EmployeeId</c> từ token), Quản lý xem theo chi nhánh (ép <c>@BranchId</c>),
    /// Admin xem được toàn hệ thống.
    /// </summary>
    Task<CommissionSummaryDto> GetSummaryAsync(
        CommissionSummaryQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Điều chỉnh hoa hồng thủ công — POST /api/v1/admin/commissions/adjustments
    /// (usp_Commission_Adjust, chỉ Admin). Bút toán điều chỉnh luôn vào kỳ đang mở; số tiền khác 0 và bắt buộc
    /// có lý do, mọi thao tác đều ghi nhật ký AUD-01.
    /// </summary>
    Task<CommissionAdjustmentResultDto> AdjustAsync(
        AdjustCommissionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chốt kỳ hoa hồng — POST /api/v1/admin/commission-periods/{id}/close (usp_Commission_ClosePeriod, chỉ Admin).
    /// Kỳ đã chốt không điều chỉnh được nữa (50002 → 409); sau khi chốt, bút toán mới thuộc kỳ đang mở kế tiếp.
    /// </summary>
    Task ClosePeriodAsync(int periodId, CancellationToken cancellationToken = default);
}