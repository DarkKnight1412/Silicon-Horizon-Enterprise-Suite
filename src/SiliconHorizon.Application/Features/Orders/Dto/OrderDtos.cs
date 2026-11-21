using SiliconHorizon.Application.Common.Models;

namespace SiliconHorizon.Application.Features.Orders.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO đơn hàng — khớp use case ORD-01..13 (BRD mục 6) và Database/README.md mục 5.
// Quy ước: API LUÔN ép customerId (khách) và branchId (nhân viên) từ token,
// nên các DTO request KHÔNG có hai trường này (trừ DTO dành riêng cho khách chọn
// chi nhánh nhận hàng, và DTO tại quầy do nhân viên chọn khách được phục vụ).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// POST /api/v1/orders — khách đặt hàng từ các dòng đã tick trong giỏ (usp_Order_Create với
/// <c>@ItemsJson = NULL</c>, <c>@Channel = 0</c>).
/// </summary>
/// <remarks>
/// Không có <c>customerId</c> trong body: API ép <c>@CustomerId</c> từ token.
/// Proc tự chốt giá, áp voucher, giữ hàng và xóa các dòng đã đặt khỏi giỏ (CART-05, ORD-04, ORD-10).
/// </remarks>
public class CreateOrderRequest
{
    /// <summary>
    /// Chi nhánh xuất hàng (giao tận nơi — ORD-01) hoặc chi nhánh khách đến nhận (ORD-02).
    /// Proc kiểm tra chi nhánh tồn tại/đang hoạt động (50001 → 404) và đủ tồn khả dụng (50003 → 409).
    /// </summary>
    /// <remarks>
    /// ⚠️ ORD-01 nói hệ thống tự chọn chi nhánh xuất (gần nhất đủ hàng / kho mặc định), nhưng <b>chưa có proc nào
    /// làm việc này</b> (usp_Order_Create nhận @BranchId từ ngoài), nên hiện tại client phải gửi chi nhánh —
    /// thường lấy từ API danh sách chi nhánh/tồn kho. Khi bổ sung proc chọn chi nhánh, trường này sẽ thành tùy chọn.
    /// </remarks>
    public int BranchId { get; set; }

    /// <summary>0 Giao tận nơi, 1 Nhận tại cửa hàng. Giao tận nơi bắt buộc đủ tên/SĐT/địa chỉ (proc báo 50004 → 400).</summary>
    public byte FulfillmentType { get; set; }

    public string? RecipientName { get; set; }

    public string? RecipientPhone { get; set; }

    public string? ShippingAddress { get; set; }

    /// <summary>Phí vận chuyển (VND); âm → 400.</summary>
    public decimal ShippingFee { get; set; }

    /// <summary>0 COD, 1 Chuyển khoản, 2 Cổng thanh toán, 3 Tiền mặt/POS (chỉ đơn tại quầy), 4 Công nợ.</summary>
    public byte PaymentMethod { get; set; }

    /// <summary>Mã giảm giá; proc kiểm tra hạn dùng, giá trị đơn tối thiểu và số lượt còn lại (50004/50006).</summary>
    public string? VoucherCode { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// POST /api/v1/pos/orders — thu ngân/tư vấn tạo đơn tại quầy (usp_Order_Create với
/// <c>@ItemsJson</c> bắt buộc, <c>@Channel = 1</c>).
/// </summary>
/// <remarks>
/// Không có <c>branchId</c> trong body: API ép <c>@BranchId</c> và <c>@UserId</c> từ token (ORD-03).
/// Đơn tại quầy không truyền <see cref="SalesStaff"/> thì proc gán nhân viên tạo đơn 100% (KPI-02).
/// </remarks>
public class CreatePosOrderRequest
{
    /// <summary>Khách được phục vụ tại quầy (bắt buộc).</summary>
    public long CustomerId { get; set; }

    /// <summary>0 Giao tận nơi, 1 Nhận tại cửa hàng (mặc định 1 — bán tại quầy, ORD-03).</summary>
    public byte FulfillmentType { get; set; } = 1;

    public string? RecipientName { get; set; }

    public string? RecipientPhone { get; set; }

    public string? ShippingAddress { get; set; }

    public decimal ShippingFee { get; set; }

    /// <summary>0 COD, 1 Chuyển khoản, 2 Cổng thanh toán, 3 Tiền mặt/POS, 4 Công nợ.</summary>
    public byte PaymentMethod { get; set; }

    public string? VoucherCode { get; set; }

    public string? Note { get; set; }

    /// <summary>Danh sách sản phẩm mua tại quầy; bắt buộc có ít nhất một dòng.</summary>
    public List<OrderItemInput> Items { get; set; } = new();

    /// <summary>
    /// Nhân viên tư vấn và tỷ lệ chia, serialize thành <c>@SalesStaffJson</c> camelCase.
    /// Tổng <c>ShareRate</c> phải bằng 100 (proc báo 50004 nếu sai).
    /// </summary>
    public List<OrderSalesStaffInput>? SalesStaff { get; set; }
}

/// <summary>Một dòng hàng gửi cho proc qua <c>@ItemsJson</c> (camelCase: <c>productId</c>, <c>quantity</c>).</summary>
public record OrderItemInput(long ProductId, int Quantity);

/// <summary>Một phần chia doanh số gửi qua <c>@SalesStaffJson</c> (camelCase: <c>employeeId</c>, <c>shareRate</c>).</summary>
public record OrderSalesStaffInput(long EmployeeId, decimal ShareRate);

/// <summary>Query của GET /api/v1/orders, /api/v1/staff/orders và /api/v1/admin/orders (usp_Order_List).</summary>
/// <remarks>
/// KHÔNG có <c>customerId</c>/<c>branchId</c>: khách bị ép <c>CustomerId</c> từ token, nhân viên bị ép
/// <c>BranchId</c> từ token, Admin xem toàn hệ thống (BranchId = <c>NULL</c>).
/// </remarks>
public class OrderListRequest : PageRequest
{
    /// <summary>Lọc theo trạng thái đơn 0..10; bỏ trống = mọi trạng thái.</summary>
    public byte? Status { get; set; }

    /// <summary>Lọc theo <c>CreatedAt</c> (UTC, ISO 8601) — tính từ mốc này trở đi.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Lọc theo <c>CreatedAt</c> (UTC) — CHẶN TRÊN, không bao gồm chính mốc này.</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Tìm theo mã đơn (khớp đầu mã), SĐT hoặc tên khách (chứa). Tối đa 50 ký tự (cột nvarchar(50) của proc).</summary>
    public string? Keyword { get; set; }
}

/// <summary>POST /api/v1/orders/{id}/cancel và /cancel-approved — bắt buộc lý do (ORD-08).</summary>
public class CancelOrderRequest
{
    /// <summary>Lý do hủy, tối đa 300 ký tự; rỗng → 400.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>POST /api/v1/orders/{id}/delivery-failed — bắt buộc lý do giao thất bại (ORD-08).</summary>
public class DeliveryFailedRequest
{
    /// <summary>Lý do giao thất bại, tối đa 300 ký tự; rỗng → 400.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>POST /api/v1/orders/{id}/serials — quét serial gán vào đơn đang đóng gói (UC-11).</summary>
public class AssignSerialRequest
{
    /// <summary>Số serial quét được, tối đa 100 ký tự.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Chỉ cần khi cùng số serial xuất hiện ở nhiều sản phẩm trong đơn; thiếu mà proc thấy trùng → 400.
    /// </summary>
    public long? ProductId { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// DTO phản hồi
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Đơn vừa tạo — result set của <c>usp_Order_Create</c>.</summary>
public class CreatedOrderDto
{
    public long OrderId { get; init; }

    public string OrderCode { get; init; } = string.Empty;

    /// <summary>0 PendingPayment (đã giữ hàng, có hạn thanh toán) hoặc 1 Pending.</summary>
    public byte Status { get; init; }

    public decimal GrandTotal { get; init; }

    /// <summary>Hạn giữ hàng / hạn nhận hàng (ORD-02, ORD-11) — UTC; <c>NULL</c> nếu đơn không giữ hàng theo thời hạn.</summary>
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>Chi tiết đơn — gom 6 result set của <c>usp_Order_Get</c> thành một DTO.</summary>
public class OrderDetailDto
{
    public OrderHeaderDto Order { get; init; } = new();

    public IReadOnlyList<OrderItemDto> Items { get; init; } = Array.Empty<OrderItemDto>();

    /// <summary>Serial đã gán vào đơn (rỗng khi đơn chưa qua bước đóng gói).</summary>
    public IReadOnlyList<OrderSerialDto> Serials { get; init; } = Array.Empty<OrderSerialDto>();

    /// <summary>Giao dịch thu/hoàn tiền của đơn (PAY-04: hoàn tiền là bản ghi riêng, không sửa giao dịch gốc).</summary>
    public IReadOnlyList<OrderPaymentDto> Payments { get; init; } = Array.Empty<OrderPaymentDto>();

    /// <summary>Lịch sử chuyển trạng thái (ORD-08).</summary>
    public IReadOnlyList<OrderStatusHistoryDto> StatusHistory { get; init; } = Array.Empty<OrderStatusHistoryDto>();

    /// <summary>Nhân viên tư vấn và tỷ lệ chia của đơn (ORD-05).</summary>
    public IReadOnlyList<OrderSalesStaffDto> SalesStaff { get; init; } = Array.Empty<OrderSalesStaffDto>();
}

/// <summary>Header đơn (result set 1 của <c>usp_Order_Get</c>).</summary>
public class OrderHeaderDto
{
    public long Id { get; init; }

    public string OrderCode { get; init; } = string.Empty;

    public long CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string? CustomerPhone { get; init; }

    /// <summary>Chi nhánh xuất hàng (ORD-03).</summary>
    public int BranchId { get; init; }

    public string BranchName { get; init; } = string.Empty;

    /// <summary>0 Online, 1 POS.</summary>
    public byte Channel { get; init; }

    /// <summary>0 Giao tận nơi, 1 Nhận tại cửa hàng.</summary>
    public byte FulfillmentType { get; init; }

    /// <summary>0..10 (xem BRD mục 6.4).</summary>
    public byte Status { get; init; }

    public string? RecipientName { get; init; }

    public string? RecipientPhone { get; init; }

    public string? ShippingAddress { get; init; }

    public decimal SubTotal { get; init; }

    public decimal DiscountTotal { get; init; }

    public decimal ShippingFee { get; init; }

    public decimal GrandTotal { get; init; }

    public decimal PaidAmount { get; init; }

    /// <summary>0 COD, 1 Chuyển khoản, 2 Cổng thanh toán, 3 Tiền mặt/POS, 4 Công nợ.</summary>
    public byte PaymentMethod { get; init; }

    /// <summary>0 Unpaid, 1 PartiallyPaid, 2 Paid, 3 Refunded.</summary>
    public byte PaymentStatus { get; init; }

    public long? VoucherId { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public string? Note { get; init; }

    public DateTime CreatedAt { get; init; }

    public long? CreatedBy { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public long? UpdatedBy { get; init; }
}

/// <summary>Một dòng hàng của đơn (snapshot tại thời điểm đặt — ORD-04).</summary>
public class OrderItemDto
{
    public long Id { get; init; }

    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public decimal DiscountAmount { get; init; }

    public int Quantity { get; init; }

    public decimal LineTotal { get; init; }

    public int WarrantyMonths { get; init; }

    public bool RequiresSerial { get; init; }

    public long? PromotionId { get; init; }

    /// <summary>Số serial đã gán cho dòng — so với <see cref="Quantity"/> để biết còn thiếu serial nào.</summary>
    public int SerialAssignedCount { get; init; }
}

/// <summary>Serial đã gán vào đơn.</summary>
public class OrderSerialDto
{
    public long SerialItemId { get; init; }

    public long OrderItemId { get; init; }

    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>0 InStock, 1 Allocated (đang trong đơn), 3 Sold (đã bán, đã kích hoạt bảo hành).</summary>
    public byte Status { get; init; }

    public DateTime? WarrantyStartDate { get; init; }

    public DateTime? WarrantyEndDate { get; init; }
}

/// <summary>Một giao dịch thanh toán của đơn (bảng Payments).</summary>
public class OrderPaymentDto
{
    public long Id { get; init; }

    /// <summary>0 Payment (thu), 1 Refund (hoàn — PAY-04).</summary>
    public byte PaymentType { get; init; }

    public byte Method { get; init; }

    public decimal Amount { get; init; }

    /// <summary>0 Pending, 1 Succeeded, 2 Failed.</summary>
    public byte Status { get; init; }

    public string? Provider { get; init; }

    public string? ProviderTransactionId { get; init; }

    /// <summary>Giao dịch gốc mà bản ghi hoàn tiền trỏ về.</summary>
    public long? ParentPaymentId { get; init; }

    public long? ReturnRequestId { get; init; }

    public DateTime? PaidAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public long? CreatedBy { get; init; }
}

/// <summary>Một lần chuyển trạng thái của đơn (ORD-08).</summary>
public class OrderStatusHistoryDto
{
    public byte? FromStatus { get; init; }

    public byte ToStatus { get; init; }

    public string? Reason { get; init; }

    /// <summary><c>NULL</c> khi do hệ thống thực hiện (job hủy đơn quá hạn).</summary>
    public long? ChangedBy { get; init; }

    public DateTime ChangedAt { get; init; }
}

/// <summary>Nhân viên tư vấn của đơn và tỷ lệ chia doanh số (KPI-01, KPI-02).</summary>
public class OrderSalesStaffDto
{
    public long EmployeeId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public decimal ShareRate { get; init; }

    public bool IsPrimary { get; init; }
}

/// <summary>Một dòng trong danh sách đơn (result set của <c>usp_Order_List</c>).</summary>
public class OrderListItemDto
{
    public long Id { get; init; }

    public string OrderCode { get; init; } = string.Empty;

    public long CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string? CustomerPhone { get; init; }

    public int BranchId { get; init; }

    public byte Channel { get; init; }

    public byte FulfillmentType { get; init; }

    public byte Status { get; init; }

    public decimal GrandTotal { get; init; }

    public decimal PaidAmount { get; init; }

    public byte PaymentMethod { get; init; }

    public byte PaymentStatus { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? ExpiresAt { get; init; }
}

/// <summary>Kết quả đổi trạng thái chỉ trả về trạng thái mới (usp_Order_MarkReady).</summary>
public class OrderStatusChangeDto
{
    /// <summary>4 ReadyForPickup hoặc 5 Shipping.</summary>
    public byte Status { get; init; }
}

/// <summary>Kết quả quét serial: tiến độ theo từng dòng hàng cần serial (usp_Order_AssignSerial).</summary>
public class OrderSerialProgressDto
{
    public long OrderItemId { get; init; }

    public long ProductId { get; init; }

    public int Quantity { get; init; }

    /// <summary>Số serial đã gán cho dòng này.</summary>
    public int Assigned { get; init; }

    /// <summary>Số serial còn thiếu để đủ điều kiện đóng gói xong (usp_Order_MarkReady kiểm tra điều này).</summary>
    public int Remaining => Quantity > Assigned ? Quantity - Assigned : 0;
}

/// <summary>Kết quả hủy đơn (usp_Order_Cancel).</summary>
public class OrderCancelResultDto
{
    public long OrderId { get; init; }

    /// <summary>8 Cancelled.</summary>
    public byte Status { get; init; }

    /// <summary>Số tiền đã thu của đơn tại thời điểm hủy.</summary>
    public decimal PaidAmount { get; init; }

    /// <summary><c>true</c> = đơn đã thu tiền, phải hoàn tiền qua POST /api/v1/orders/{id}/refunds (PAY-04).</summary>
    public bool RefundRequired { get; init; }
}
