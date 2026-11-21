using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Warranty.Dto;

namespace SiliconHorizon.Application.Features.Warranty;

/// <summary>
/// Use case bảo hành — WAR-04..WAR-07 và Database/README.md mục 7.1 (UC-16, UC-17).
/// Mọi phương thức chỉ gọi stored procedure trong <c>Database/05_sp_warranty_returns_commission.sql</c>.
/// </summary>
/// <remarks>
/// Vòng đời phiếu (<c>WarrantyTickets.Status</c>):
/// 0 Received → 1 Diagnosing → 2 SentToVendor → (3 Repaired | 4 Replaced | 5 Rejected) → 6 ReturnedToCustomer.
/// <para>
/// Phạm vi dữ liệu: nhân viên ép <c>@BranchId</c> từ token (thiếu → 403), khách ép <c>@CustomerId</c> từ token.
/// </para>
/// </remarks>
public interface IWarrantyService
{
    /// <summary>
    /// Tiếp nhận bảo hành — POST /api/v1/warranty-tickets (usp_Warranty_Receive, WAR-04/WAR-05).
    /// Chi nhánh tiếp nhận ép từ token. Serial phải đang Sold; hết hạn bảo hành hoặc có <c>rejectReason</c>
    /// thì proc tạo phiếu ở trạng thái Rejected (5) và giữ nguyên serial.
    /// </summary>
    Task<ReceivedWarrantyTicketDto> ReceiveAsync(
        ReceiveWarrantyTicketRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuyển trạng thái phiếu — POST /api/v1/warranty-tickets/{id}/status
    /// (usp_Warranty_ChangeStatus, WAR-06/WAR-07). Đổi mới bắt buộc kèm serial thay thế; từ chối bắt buộc kèm lý do.
    /// </summary>
    Task ChangeStatusAsync(
        long ticketId,
        ChangeWarrantyStatusRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đính kèm ảnh/tài liệu — POST /api/v1/warranty-tickets/{id}/attachments (usp_Warranty_AddAttachment).
    /// </summary>
    Task AddAttachmentAsync(
        long ticketId,
        AddWarrantyAttachmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Danh sách phiếu — GET /api/v1/warranty-tickets (usp_Warranty_List). Nhân viên xem theo chi nhánh,
    /// khách chỉ xem phiếu của mình (proc nhận <c>@BranchId</c> hoặc <c>@CustomerId</c> ép từ token).
    /// </summary>
    Task<PagedResult<WarrantyTicketListItemDto>> ListAsync(
        WarrantyTicketListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chi tiết phiếu kèm lịch sử trạng thái và đính kèm — GET /api/v1/warranty-tickets/{id}
    /// (usp_Warranty_Get, 3 result set). Khách chỉ xem được phiếu của mình (proc lọc theo <c>@CustomerId</c>).
    /// </summary>
    Task<WarrantyTicketDetailDto> GetAsync(long ticketId, CancellationToken cancellationToken = default);
}