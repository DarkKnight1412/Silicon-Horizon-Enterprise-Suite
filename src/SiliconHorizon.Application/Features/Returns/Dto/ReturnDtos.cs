namespace SiliconHorizon.Application.Features.Returns.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO đổi trả — khớp use case RET-01..05 (BRD) và Database/README.md mục 7.2.
//
// Quy ước phạm vi dữ liệu: khách ép @CustomerId từ token (chỉ thấy yêu cầu của mình),
// nhân viên ép @BranchId từ token — DTO request KHÔNG có hai trường này.
//
// Lớp *Row khớp CHÍNH XÁC tên cột của từng result set trong
// Database/05_sp_warranty_returns_commission.sql; lớp *Dto là dữ liệu trả ra API.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Yêu cầu đổi trả đầy đủ — result set 1 của <c>dbo.usp_Return_Get</c>
/// (<c>ReturnRequests rq.*</c> kèm <c>Orders.OrderCode</c>). Cũng dùng làm header khi kiểm tra phạm vi chi nhánh.
/// </summary>
public sealed class ReturnRequestRow
{
    public long Id { get; set; }

    public string ReturnCode { get; set; } = string.Empty;

    public long OrderId { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public long CustomerId { get; set; }

    public int BranchId { get; set; }

    /// <summary>0 Requested … 5 Closed (xem <see cref="ReturnStatusText"/> và ReturnRequestStatus).</summary>
    public byte Status { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Tiền hoàn dự kiến do <c>usp_Return_Create</c> tính (đã trừ phần voucher phân bổ, không gồm phí vận chuyển).</summary>
    public decimal RefundAmount { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}

/// <summary>Một dòng hàng trả — result set 2 của <c>dbo.usp_Return_Get</c>.</summary>
public sealed class ReturnItemRow
{
    public long Id { get; set; }

    public long OrderItemId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public long? SerialItemId { get; set; }

    public string? SerialNumber { get; set; }

    public int Quantity { get; set; }

    /// <summary>0 Pending, 1 Pass, 2 Fail.</summary>
    public byte InspectionResult { get; set; }
}

/// <summary>Một dòng danh sách yêu cầu — result set của <c>dbo.usp_Return_List</c>.</summary>
public sealed class ReturnListItemRow
{
    public long Id { get; set; }

    public string ReturnCode { get; set; } = string.Empty;

    public long OrderId { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public long CustomerId { get; set; }

    public int BranchId { get; set; }

    public byte Status { get; set; }

    public decimal RefundAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Cột <c>COUNT(*) OVER ()</c> để phân trang theo quy ước Database/README.md mục 1.1.</summary>
    public int TotalCount { get; set; }
}

/// <summary>Kết quả tạo yêu cầu đổi trả — result set của <c>dbo.usp_Return_Create</c>.</summary>
public sealed class CreatedReturnRow
{
    public long ReturnRequestId { get; set; }

    public string ReturnCode { get; set; } = string.Empty;

    /// <summary>Tiền hoàn dự kiến (chưa phải số tiền đã hoàn).</summary>
    public decimal RefundAmount { get; set; }
}

/// <summary>Kết quả hoàn tiền — result set của <c>dbo.usp_Return_Refund</c>.</summary>
public sealed class ReturnRefundResultRow
{
    public long ReturnRequestId { get; set; }

    /// <summary>Số tiền hoàn thực tế (bằng <c>@Amount</c> truyền vào, hoặc số dự kiến của yêu cầu).</summary>
    public decimal RefundAmount { get; set; }

    /// <summary>Phần hoàn bằng tiền mặt/chuyển khoản (không vượt quá số đã thu của đơn).</summary>
    public decimal CashRefunded { get; set; }

    /// <summary>Phần giảm trừ vào công nợ — chỉ khác 0 khi đơn mua chịu (PaymentMethod = 4).</summary>
    public decimal DebtReduced { get; set; }

    /// <summary>9 PartiallyReturned hoặc 10 Returned.</summary>
    public byte OrderStatus { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Dữ liệu trả ra API
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Yêu cầu đổi trả trả ra API — GET /api/v1/returns/{id}.</summary>
public class ReturnRequestDto
{
    public long Id { get; init; }

    public string ReturnCode { get; init; } = string.Empty;

    public long OrderId { get; init; }

    public string OrderCode { get; init; } = string.Empty;

    public long CustomerId { get; init; }

    public int BranchId { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public decimal RefundAmount { get; init; }

    public long? ApprovedBy { get; init; }

    public DateTime? ApprovedAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static ReturnRequestDto FromRow(ReturnRequestRow row) => new()
    {
        Id = row.Id,
        ReturnCode = row.ReturnCode,
        OrderId = row.OrderId,
        OrderCode = row.OrderCode,
        CustomerId = row.CustomerId,
        BranchId = row.BranchId,
        Status = row.Status,
        StatusName = ReturnStatusText.StatusText(row.Status),
        Reason = row.Reason,
        RefundAmount = row.RefundAmount,
        ApprovedBy = row.ApprovedBy,
        ApprovedAt = row.ApprovedAt,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt
    };
}

/// <summary>Một dòng hàng trả (kèm kết quả kiểm tra).</summary>
public class ReturnItemDto
{
    public long Id { get; init; }

    public long OrderItemId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public long? SerialItemId { get; init; }

    public string? SerialNumber { get; init; }

    public int Quantity { get; init; }

    public byte InspectionResult { get; init; }

    public string InspectionResultName { get; init; } = string.Empty;

    public static ReturnItemDto FromRow(ReturnItemRow row) => new()
    {
        Id = row.Id,
        OrderItemId = row.OrderItemId,
        Sku = row.Sku,
        ProductName = row.ProductName,
        SerialItemId = row.SerialItemId,
        SerialNumber = row.SerialNumber,
        Quantity = row.Quantity,
        InspectionResult = row.InspectionResult,
        InspectionResultName = ReturnStatusText.InspectionResultText(row.InspectionResult)
    };
}

/// <summary>Chi tiết yêu cầu đổi trả kèm các dòng hàng trả.</summary>
public class ReturnRequestDetailDto
{
    public ReturnRequestDto Request { get; init; } = new();

    public IReadOnlyList<ReturnItemDto> Items { get; init; } = Array.Empty<ReturnItemDto>();
}

/// <summary>Một dòng trong danh sách yêu cầu đổi trả.</summary>
public class ReturnListItemDto
{
    public long Id { get; init; }

    public string ReturnCode { get; init; } = string.Empty;

    public long OrderId { get; init; }

    public string OrderCode { get; init; } = string.Empty;

    public long CustomerId { get; init; }

    public int BranchId { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public decimal RefundAmount { get; init; }

    public DateTime CreatedAt { get; init; }

    public static ReturnListItemDto FromRow(ReturnListItemRow row) => new()
    {
        Id = row.Id,
        ReturnCode = row.ReturnCode,
        OrderId = row.OrderId,
        OrderCode = row.OrderCode,
        CustomerId = row.CustomerId,
        BranchId = row.BranchId,
        Status = row.Status,
        StatusName = ReturnStatusText.StatusText(row.Status),
        RefundAmount = row.RefundAmount,
        CreatedAt = row.CreatedAt
    };
}

/// <summary>Kết quả tạo yêu cầu đổi trả — POST /api/v1/orders/{id}/returns.</summary>
public class CreatedReturnDto
{
    public long ReturnRequestId { get; init; }

    public string ReturnCode { get; init; } = string.Empty;

    /// <summary>Tiền hoàn dự kiến: giá dòng sau khuyến mãi trừ phần voucher phân bổ theo tỷ lệ, không gồm phí vận chuyển.</summary>
    public decimal RefundAmount { get; init; }

    /// <summary>Yêu cầu luôn được tạo ở trạng thái Requested (0) — chờ Quản lý duyệt.</summary>
    public byte Status => (byte)Domain.Enums.ReturnRequestStatus.Requested;

    public static CreatedReturnDto FromRow(CreatedReturnRow row) => new()
    {
        ReturnRequestId = row.ReturnRequestId,
        ReturnCode = row.ReturnCode,
        RefundAmount = row.RefundAmount
    };
}

/// <summary>Kết quả hoàn tiền — POST /api/v1/returns/{id}/refund.</summary>
public class ReturnRefundResultDto
{
    public long ReturnRequestId { get; init; }

    public decimal RefundAmount { get; init; }

    public decimal CashRefunded { get; init; }

    public decimal DebtReduced { get; init; }

    public byte OrderStatus { get; init; }

    public string OrderStatusName { get; init; } = string.Empty;

    /// <summary><c>true</c> = đã trả hết số lượng đã mua nên đơn chuyển sang Returned (10).</summary>
    public bool FullyReturned => OrderStatus == (byte)Domain.Enums.OrderStatus.Returned;

    public static ReturnRefundResultDto FromRow(ReturnRefundResultRow row) => new()
    {
        ReturnRequestId = row.ReturnRequestId,
        RefundAmount = row.RefundAmount,
        CashRefunded = row.CashRefunded,
        DebtReduced = row.DebtReduced,
        OrderStatus = row.OrderStatus,
        OrderStatusName = ReturnStatusText.OrderStatusText(row.OrderStatus)
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Tham số request
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// POST /api/v1/orders/{id}/returns — khách tạo yêu cầu đổi trả (usp_Return_Create, RET-01).
/// </summary>
/// <remarks>
/// Không có <c>customerId</c>/<c>branchId</c> trong body: khách ép từ token, chi nhánh do proc lấy theo đơn.
/// Chỉ đổi trả được với đơn đã hoàn tất và trong hạn đổi trả
/// (<c>SystemSettings['return.window.days']</c>, mặc định <b>7 ngày</b> kể từ ngày hoàn tất — hết hạn → 409).
/// </remarks>
public class CreateReturnRequest
{
    /// <summary>Lý do đổi trả — bắt buộc, tối đa 500 ký tự (ReturnRequests.Reason nvarchar(500)).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Danh sách dòng hàng trả — phải có ít nhất một dòng.</summary>
    public IReadOnlyList<ReturnItemInput> Items { get; set; } = Array.Empty<ReturnItemInput>();
}

/// <summary>Một dòng hàng trả trong yêu cầu đổi trả (phần tử của <c>@ItemsJson</c>).</summary>
public class ReturnItemInput
{
    /// <summary>Dòng hàng của đơn gốc (OrderItems.Id) — phải thuộc đơn đang đổi trả.</summary>
    public long OrderItemId { get; set; }

    /// <summary>
    /// Số lượng trả; tổng số lượng đã trả ở các yêu cầu còn hiệu lực (khác Rejected) không được vượt
    /// số lượng đã mua (proc chặn, 50006 → 409).
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Danh sách số serial trả — BẮT BUỘC và phải đủ bằng <see cref="Quantity"/> với hàng quản lý theo serial;
    /// hàng không quản lý theo serial thì để trống. Serial phải đang ở trạng thái Sold và chưa nằm trong
    /// yêu cầu đổi trả nào khác.
    /// </summary>
    public IReadOnlyList<string>? SerialNumbers { get; set; }
}

/// <summary>POST /api/v1/returns/{id}/review — Quản lý duyệt hoặc từ chối (usp_Return_Review, RET-04).</summary>
public class ReviewReturnRequest
{
    /// <summary><c>true</c> = duyệt (Approved), <c>false</c> = từ chối (Rejected).</summary>
    public bool Approve { get; set; }

    /// <summary>Lý do — BẮT BUỘC khi từ chối, tối đa 300 ký tự (proc báo 50004 → 400 nếu thiếu).</summary>
    public string? Note { get; set; }
}

/// <summary>POST /api/v1/returns/{id}/inspect — thủ kho nhận hàng và kiểm tra (usp_Return_Inspect, RET-03).</summary>
public class InspectReturnRequest
{
    /// <summary>
    /// Kết quả kiểm tra cho <b>tất cả</b> dòng của yêu cầu (proc báo 50004 nếu thiếu dòng).
    /// </summary>
    public IReadOnlyList<InspectReturnItemInput> Results { get; set; } = Array.Empty<InspectReturnItemInput>();
}

/// <summary>Kết quả kiểm tra một dòng hàng trả (phần tử của <c>@ResultsJson</c>).</summary>
public class InspectReturnItemInput
{
    /// <summary>Id dòng hàng trả (ReturnItems.Id lấy từ GET /api/v1/returns/{id}).</summary>
    public long ReturnItemId { get; set; }

    /// <summary>1 = đạt (serial về InStock, cộng tồn), 2 = không đạt (serial về Defective).</summary>
    public byte Result { get; set; }
}

/// <summary>POST /api/v1/returns/{id}/refund — hoàn tiền và thu hồi hoa hồng (usp_Return_Refund, RET-05).</summary>
public class RefundReturnRequest
{
    /// <summary>
    /// Phương thức hoàn tiền (Payments.Method) — dùng cùng quy ước <c>PaymentMethod</c> của đơn:
    /// 0 COD, 1 Chuyển khoản, 2 Cổng thanh toán, 3 Tiền mặt/POS, 4 Công nợ.
    /// </summary>
    public byte Method { get; set; }

    /// <summary>
    /// Số tiền hoàn — bỏ trống thì hoàn đúng số dự kiến của yêu cầu (<c>RefundAmount</c>).
    /// Proc chỉ nhận giá trị trong khoảng 0..số dự kiến (lớn hơn → 50006 → 409). Dùng trường này khi
    /// muốn giảm số hoàn với hàng kiểm tra không đạt (Database/README.md mục "Lưu ý nghiệp vụ").
    /// </summary>
    public decimal? Amount { get; set; }
}

/// <summary>
/// Tham số lọc danh sách yêu cầu đổi trả của GET /api/v1/returns.
/// KHÔNG có <c>branchId</c>/<c>customerId</c>: phạm vi do API ép từ token theo vai trò người gọi.
/// </summary>
public class ReturnListQuery
{
    /// <summary>Lọc theo trạng thái yêu cầu (0..5), bỏ trống = tất cả.</summary>
    public byte? Status { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}