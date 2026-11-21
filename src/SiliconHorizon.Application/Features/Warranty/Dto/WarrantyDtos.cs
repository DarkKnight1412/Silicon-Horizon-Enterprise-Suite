namespace SiliconHorizon.Application.Features.Warranty.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO bảo hành — khớp use case WAR-01..08 (BRD) và Database/README.md mục 7.1.
//
// Quy ước phạm vi dữ liệu (Database/README.md mục 1.1, BRD mục 2.2):
//   • Nhân viên: API ép @BranchId (chi nhánh của nhân viên) từ token — DTO request KHÔNG có branchId.
//   • Khách hàng: API ép @CustomerId từ token — chỉ xem/tra cứu hàng của mình.
//
// Lớp *Row khớp CHÍNH XÁC tên cột của từng result set trong
// Database/05_sp_warranty_returns_commission.sql; lớp *Dto là dữ liệu trả ra API.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Phiếu bảo hành đầy đủ — result set 1 của <c>dbo.usp_Warranty_Get</c>
/// (<c>WarrantyTickets t.*</c> kèm các cột join: serial, sản phẩm, khách, serial thay thế).
/// </summary>
/// <remarks>Kiểu này cũng dùng làm header khi chỉ cần kiểm tra phạm vi chi nhánh của phiếu.</remarks>
public sealed class WarrantyTicketRow
{
    public long Id { get; set; }

    public string TicketCode { get; set; } = string.Empty;

    /// <summary>Hàng có serial (NULL với hàng không quản lý serial).</summary>
    public long? SerialItemId { get; set; }

    /// <summary>Hàng không serial (NULL với hàng có serial).</summary>
    public long? OrderItemId { get; set; }

    public long CustomerId { get; set; }

    public int BranchId { get; set; }

    /// <summary>0 Received … 6 ReturnedToCustomer (xem <see cref="WarrantyStatusText"/>).</summary>
    public byte Status { get; set; }

    public string IssueDescription { get; set; } = string.Empty;

    public string? ConditionNotes { get; set; }

    public string? RejectionReason { get; set; }

    public long? ReplacementSerialId { get; set; }

    public DateTime? VendorSentAt { get; set; }

    public DateTime? VendorReturnedAt { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }

    /// <summary>Phí sửa chữa thu của khách (0 nếu trong hạn bảo hành).</summary>
    public decimal Fee { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    /// <summary>Số serial của hàng gửi bảo hành (NULL với hàng không serial).</summary>
    public string? SerialNumber { get; set; }

    public string? Sku { get; set; }

    public string? ProductName { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string? CustomerPhone { get; set; }

    /// <summary>Số serial của hàng đổi mới (chỉ có khi Status = 4 Replaced).</summary>
    public string? ReplacementSerialNumber { get; set; }
}

/// <summary>Một lần chuyển trạng thái — result set 2 của <c>dbo.usp_Warranty_Get</c> (WarrantyTicketHistory).</summary>
public sealed class WarrantyTicketHistoryRow
{
    public byte? FromStatus { get; set; }

    public byte ToStatus { get; set; }

    /// <summary>Proc ghi <c>@Note</c>, hoặc lý do từ chối nếu không truyền ghi chú.</summary>
    public string? Note { get; set; }

    public long? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }
}

/// <summary>Ảnh/tài liệu đính kèm — result set 3 của <c>dbo.usp_Warranty_Get</c> (WarrantyTicketAttachments).</summary>
public sealed class WarrantyTicketAttachmentRow
{
    public long Id { get; set; }

    public string Url { get; set; } = string.Empty;
}

/// <summary>Một dòng danh sách phiếu — result set của <c>dbo.usp_Warranty_List</c>.</summary>
public sealed class WarrantyTicketListItemRow
{
    public long Id { get; set; }

    public string TicketCode { get; set; } = string.Empty;

    public byte Status { get; set; }

    public int BranchId { get; set; }

    public long CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string? SerialNumber { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }

    /// <summary>Cột <c>COUNT(*) OVER ()</c> để phân trang theo quy ước Database/README.md mục 1.1.</summary>
    public int TotalCount { get; set; }
}

/// <summary>Kết quả tiếp nhận bảo hành — result set của <c>dbo.usp_Warranty_Receive</c>.</summary>
public sealed class WarrantyReceiveResultRow
{
    public long TicketId { get; set; }

    public string TicketCode { get; set; } = string.Empty;

    public byte Status { get; set; }

    /// <summary>Lý do từ chối khi proc tự từ chối (hết hạn bảo hành hoặc nhân viên nhập <c>rejectReason</c>).</summary>
    public string? RejectionReason { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Dữ liệu trả ra API
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Phiếu bảo hành trả ra API — GET /api/v1/warranty-tickets/{id}.</summary>
public class WarrantyTicketDto
{
    public long Id { get; init; }

    public string TicketCode { get; init; } = string.Empty;

    public long? SerialItemId { get; init; }

    public long? OrderItemId { get; init; }

    public long CustomerId { get; init; }

    public int BranchId { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public string IssueDescription { get; init; } = string.Empty;

    public string? ConditionNotes { get; init; }

    public string? RejectionReason { get; init; }

    public long? ReplacementSerialId { get; init; }

    public string? ReplacementSerialNumber { get; init; }

    public DateTime? VendorSentAt { get; init; }

    public DateTime? VendorReturnedAt { get; init; }

    public DateTime ReceivedAt { get; init; }

    public DateTime? ReturnedAt { get; init; }

    public decimal Fee { get; init; }

    public string? SerialNumber { get; init; }

    public string? Sku { get; init; }

    public string? ProductName { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string? CustomerPhone { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static WarrantyTicketDto FromRow(WarrantyTicketRow row) => new()
    {
        Id = row.Id,
        TicketCode = row.TicketCode,
        SerialItemId = row.SerialItemId,
        OrderItemId = row.OrderItemId,
        CustomerId = row.CustomerId,
        BranchId = row.BranchId,
        Status = row.Status,
        StatusName = WarrantyStatusText.StatusText(row.Status),
        IssueDescription = row.IssueDescription,
        ConditionNotes = row.ConditionNotes,
        RejectionReason = row.RejectionReason,
        ReplacementSerialId = row.ReplacementSerialId,
        ReplacementSerialNumber = row.ReplacementSerialNumber,
        VendorSentAt = row.VendorSentAt,
        VendorReturnedAt = row.VendorReturnedAt,
        ReceivedAt = row.ReceivedAt,
        ReturnedAt = row.ReturnedAt,
        Fee = row.Fee,
        SerialNumber = row.SerialNumber,
        Sku = row.Sku,
        ProductName = row.ProductName,
        CustomerName = row.CustomerName,
        CustomerPhone = row.CustomerPhone,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt
    };
}

/// <summary>Một lần chuyển trạng thái phiếu bảo hành.</summary>
public class WarrantyTicketHistoryDto
{
    public byte? FromStatus { get; init; }

    public string? FromStatusName { get; init; }

    public byte ToStatus { get; init; }

    public string ToStatusName { get; init; } = string.Empty;

    public string? Note { get; init; }

    public long? ChangedBy { get; init; }

    public DateTime ChangedAt { get; init; }

    public static WarrantyTicketHistoryDto FromRow(WarrantyTicketHistoryRow row) => new()
    {
        FromStatus = row.FromStatus,
        FromStatusName = row.FromStatus is { } from ? WarrantyStatusText.StatusText(from) : null,
        ToStatus = row.ToStatus,
        ToStatusName = WarrantyStatusText.StatusText(row.ToStatus),
        Note = row.Note,
        ChangedBy = row.ChangedBy,
        ChangedAt = row.ChangedAt
    };
}

/// <summary>Ảnh/tài liệu đính kèm của phiếu bảo hành.</summary>
public class WarrantyTicketAttachmentDto
{
    public long Id { get; init; }

    public string Url { get; init; } = string.Empty;
}

/// <summary>Chi tiết phiếu kèm lịch sử trạng thái và danh sách đính kèm.</summary>
public class WarrantyTicketDetailDto
{
    public WarrantyTicketDto Ticket { get; init; } = new();

    public IReadOnlyList<WarrantyTicketHistoryDto> History { get; init; } = Array.Empty<WarrantyTicketHistoryDto>();

    public IReadOnlyList<WarrantyTicketAttachmentDto> Attachments { get; init; } = Array.Empty<WarrantyTicketAttachmentDto>();
}

/// <summary>Một dòng trong danh sách phiếu bảo hành.</summary>
public class WarrantyTicketListItemDto
{
    public long Id { get; init; }

    public string TicketCode { get; init; } = string.Empty;

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public int BranchId { get; init; }

    public long CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string? SerialNumber { get; init; }

    public DateTime ReceivedAt { get; init; }

    public DateTime? ReturnedAt { get; init; }

    public static WarrantyTicketListItemDto FromRow(WarrantyTicketListItemRow row) => new()
    {
        Id = row.Id,
        TicketCode = row.TicketCode,
        Status = row.Status,
        StatusName = WarrantyStatusText.StatusText(row.Status),
        BranchId = row.BranchId,
        CustomerId = row.CustomerId,
        CustomerName = row.CustomerName,
        SerialNumber = row.SerialNumber,
        ReceivedAt = row.ReceivedAt,
        ReturnedAt = row.ReturnedAt
    };
}

/// <summary>Kết quả tiếp nhận bảo hành — POST /api/v1/warranty-tickets (usp_Warranty_Receive).</summary>
public class ReceivedWarrantyTicketDto
{
    public long TicketId { get; init; }

    public string TicketCode { get; init; } = string.Empty;

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public string? RejectionReason { get; init; }

    /// <summary>
    /// <c>true</c> = phiếu bị từ chối ngay khi tiếp nhận: hết hạn bảo hành (WAR-05) hoặc nhân viên
    /// chủ động nhập <c>rejectReason</c> (rơi vỡ, mất tem...). Khi đó phiếu ở trạng thái Rejected (5)
    /// và serial giữ nguyên trạng thái Sold.
    /// </summary>
    public bool Rejected => Status == WarrantyStatusText.Rejected;

    public static ReceivedWarrantyTicketDto FromRow(WarrantyReceiveResultRow row) => new()
    {
        TicketId = row.TicketId,
        TicketCode = row.TicketCode,
        Status = row.Status,
        StatusName = WarrantyStatusText.StatusText(row.Status),
        RejectionReason = row.RejectionReason
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Tham số request
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// POST /api/v1/warranty-tickets — tiếp nhận bảo hành (usp_Warranty_Receive, WAR-04/WAR-05).
/// </summary>
/// <remarks>
/// Hàng có serial: truyền <see cref="SerialNumber"/> (kèm <see cref="ProductId"/> nếu cùng số serial tồn tại
/// ở nhiều sản phẩm — serial phải đang ở trạng thái Sold). Hàng không serial: truyền <see cref="OrderItemId"/>
/// của đơn đã hoàn tất (bảo hành tính từ ngày hoàn tất đơn — WAR-03).
/// Không có <c>branchId</c> trong body: chi nhánh tiếp nhận ép từ token.
/// </remarks>
public class ReceiveWarrantyTicketRequest
{
    /// <summary>Số serial của hàng bảo hành (bắt buộc khi hàng quản lý theo serial), tối đa 100 ký tự.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Chỉ cần khi cùng một số serial tồn tại ở nhiều sản phẩm.</summary>
    public long? ProductId { get; set; }

    /// <summary>Dòng hàng của đơn (bắt buộc khi hàng KHÔNG quản lý theo serial).</summary>
    public long? OrderItemId { get; set; }

    /// <summary>Mô tả lỗi khách báo — bắt buộc, tối đa 1000 ký tự (<c>@IssueDescription nvarchar(1000)</c>).</summary>
    public string IssueDescription { get; set; } = string.Empty;

    /// <summary>Tình trạng ngoại quan khi nhận, tối đa 1000 ký tự (<c>@ConditionNotes nvarchar(1000)</c>).</summary>
    public string? ConditionNotes { get; set; }

    /// <summary>
    /// Lý do từ chối nhập thẳng, tối đa 500 ký tự (<c>@RejectReason nvarchar(500)</c>) — dùng khi hết hạn
    /// bảo hành, mất tem, rơi vỡ... Có giá trị này thì proc tạo phiếu ở trạng thái Rejected (5) thay vì
    /// Received (0) và <b>không</b> chuyển serial sang RMA.
    /// </summary>
    public string? RejectReason { get; set; }
}

/// <summary>
/// POST /api/v1/warranty-tickets/{id}/status — chuyển trạng thái phiếu
/// (usp_Warranty_ChangeStatus, WAR-06/WAR-07).
/// </summary>
public class ChangeWarrantyStatusRequest
{
    /// <summary>
    /// 1 Chẩn đoán, 2 Gửi hãng, 3 Sửa xong, 4 Đổi mới, 5 Từ chối, 6 Trả khách.
    /// Cặp chuyển đổi hợp lệ: 0→1,5 | 1→2,3,4,5 | 2→3,4,5 | 3,4,5→6. Proc kiểm tra lại;
    /// chuyển sai → SQL 50002 → HTTP 409.
    /// </summary>
    public byte ToStatus { get; set; }

    /// <summary>Ghi chú xử lý (tối đa 500 ký tự) — lưu vào WarrantyTicketHistory.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// Số serial thay thế — BẮT BUỘC khi <see cref="ToStatus"/> = 4 (Đổi mới). Serial phải cùng sản phẩm
    /// với hàng bảo hành và đang InStock tại chi nhánh nhận; serial cũ → Replaced, serial mới → Sold và
    /// kế thừa hạn bảo hành còn lại (tồn thực tế của chi nhánh giảm 1).
    /// </summary>
    public string? ReplacementSerialNumber { get; set; }

    /// <summary>Lý do — BẮT BUỘC khi <see cref="ToStatus"/> = 5 (Từ chối), tối đa 500 ký tự.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Phí sửa chữa thu của khách; bỏ trống (null) thì proc giữ nguyên phí hiện tại của phiếu.</summary>
    public decimal? Fee { get; set; }
}

/// <summary>POST /api/v1/warranty-tickets/{id}/attachments — đính kèm ảnh/tài liệu (usp_Warranty_AddAttachment).</summary>
public class AddWarrantyAttachmentRequest
{
    /// <summary>Đường dẫn tài liệu, bắt buộc, tối đa 500 ký tự (WarrantyTicketAttachments.Url varchar(500)).</summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Tham số lọc danh sách phiếu bảo hành của GET /api/v1/warranty-tickets.
/// KHÔNG có <c>branchId</c>/<c>customerId</c>: phạm vi do API ép từ token theo vai trò người gọi.
/// </summary>
public class WarrantyTicketListQuery
{
    /// <summary>Lọc theo trạng thái phiếu (0..6), bỏ trống = tất cả.</summary>
    public byte? Status { get; set; }

    /// <summary>Tìm theo mã phiếu (khớp đầu mã), số serial (khớp đầu) hoặc SĐT khách — tối đa 100 ký tự.</summary>
    public string? Keyword { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}