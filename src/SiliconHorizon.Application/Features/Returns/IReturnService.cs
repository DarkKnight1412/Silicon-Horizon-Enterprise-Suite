using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Returns.Dto;

namespace SiliconHorizon.Application.Features.Returns;

/// <summary>
/// Use case đổi trả — RET-01..05 và Database/README.md mục 7.2 (UC-08, UC-18, UC-20).
/// Mọi phương thức chỉ gọi stored procedure trong <c>Database/05_sp_warranty_returns_commission.sql</c>.
/// </summary>
/// <remarks>
/// Vòng đời yêu cầu (<c>ReturnRequests.Status</c>):
/// 0 Requested → (1 Approved | 2 Rejected) → 3 GoodsReceived → 4 Refunded → 5 Closed.
/// <para>
/// Phạm vi dữ liệu: khách ép <c>@CustomerId</c> từ token (tạo yêu cầu và xem yêu cầu của mình),
/// nhân viên ép <c>@BranchId</c> từ token (thiếu → 403).
/// </para>
/// </remarks>
public interface IReturnService
{
    /// <summary>
    /// Khách tạo yêu cầu đổi trả — POST /api/v1/orders/{id}/returns (usp_Return_Create, RET-01).
    /// Chỉ áp dụng cho đơn đã hoàn tất (7 Completed / 9 PartiallyReturned) trong hạn đổi trả
    /// (<c>SystemSettings['return.window.days']</c>, mặc định 7 ngày); tiền hoàn dự kiến do proc tính.
    /// </summary>
    Task<CreatedReturnDto> CreateAsync(
        long orderId,
        CreateReturnRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Danh sách yêu cầu — GET /api/v1/returns (usp_Return_List). Nhân viên xem theo chi nhánh,
    /// khách chỉ xem yêu cầu của mình.
    /// </summary>
    Task<PagedResult<ReturnListItemDto>> ListAsync(
        ReturnListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chi tiết yêu cầu kèm các dòng hàng trả — GET /api/v1/returns/{id} (usp_Return_Get, 2 result set).
    /// </summary>
    Task<ReturnRequestDetailDto> GetAsync(long returnRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quản lý duyệt hoặc từ chối — POST /api/v1/returns/{id}/review (usp_Return_Review, RET-04).
    /// Chỉ duyệt được yêu cầu đang ở trạng thái Requested; từ chối bắt buộc kèm lý do.
    /// </summary>
    Task ReviewAsync(
        long returnRequestId,
        ReviewReturnRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thủ kho nhận hàng và kiểm tra từng dòng — POST /api/v1/returns/{id}/inspect (usp_Return_Inspect, RET-03).
    /// Hàng đạt về InStock (cộng tồn), hàng không đạt → Defective; yêu cầu chuyển GoodsReceived.
    /// </summary>
    Task InspectAsync(
        long returnRequestId,
        InspectReturnRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hoàn tiền — POST /api/v1/returns/{id}/refund (usp_Return_Refund, RET-05): ghi giao dịch hoàn,
    /// giảm nợ (nếu mua chịu), chuyển đơn sang PartiallyReturned/Returned và <b>thu hồi hoa hồng</b>
    /// theo tỷ lệ đã trả (<c>usp_Internal_ClawbackCommission</c>, KPI-04).
    /// </summary>
    Task<ReturnRefundResultDto> RefundAsync(
        long returnRequestId,
        RefundReturnRequest request,
        CancellationToken cancellationToken = default);
}