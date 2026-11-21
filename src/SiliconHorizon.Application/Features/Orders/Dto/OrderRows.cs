namespace SiliconHorizon.Application.Features.Orders.Dto;

// Lớp ánh xạ cột result set của các proc đơn hàng (Database/03_sp_orders_payments.sql) —
// tên property PHẢI khớp đúng tên cột. Dùng property get; set; thường (không phải init) để Dapper gán được.
// Cột không khai báo trong lớp (ví dụ RowVersion) được Dapper bỏ qua.

/// <summary>
/// Result set 1 của <c>usp_Order_Get</c> (header). Cũng dùng khi chỉ cần biết chủ đơn / chi nhánh / trạng thái
/// để ép phạm vi dữ liệu trước khi gọi proc chuyển trạng thái.
/// </summary>
public class OrderHeaderRow
{
    public long Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public long CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string? CustomerPhone { get; set; }

    public int BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    /// <summary>0 Online, 1 POS.</summary>
    public byte Channel { get; set; }

    /// <summary>0 Giao tận nơi, 1 Nhận tại cửa hàng.</summary>
    public byte FulfillmentType { get; set; }

    /// <summary>0..10 — xem <c>OrderStatus</c>.</summary>
    public byte Status { get; set; }

    public string? RecipientName { get; set; }

    public string? RecipientPhone { get; set; }

    public string? ShippingAddress { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal ShippingFee { get; set; }

    public decimal GrandTotal { get; set; }

    public decimal PaidAmount { get; set; }

    public byte PaymentMethod { get; set; }

    public byte PaymentStatus { get; set; }

    public long? VoucherId { get; set; }

    /// <summary>Đồng hồ đếm ngược giữ hàng (đơn PendingPayment) hoặc hạn khách đến nhận (đơn ReadyForPickup).</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}

/// <summary>Result set 2 của <c>usp_Order_Get</c> — dòng hàng đã snapshot giá và thời hạn bảo hành (ORD-04).</summary>
public class OrderItemRow
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }

    public int WarrantyMonths { get; set; }

    public bool RequiresSerial { get; set; }

    public long? PromotionId { get; set; }

    /// <summary>Số serial đã gán cho dòng (Allocated/Sold) — dùng cho màn hình đóng gói (UC-11).</summary>
    public int SerialAssignedCount { get; set; }
}

/// <summary>Result set 3 của <c>usp_Order_Get</c> — serial đã gán vào đơn.</summary>
public class OrderSerialRow
{
    public long SerialItemId { get; set; }

    public long OrderItemId { get; set; }

    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>0 InStock, 1 Allocated, 3 Sold.</summary>
    public byte Status { get; set; }

    public DateTime? WarrantyStartDate { get; set; }

    public DateTime? WarrantyEndDate { get; set; }
}

/// <summary>Result set 4 của <c>usp_Order_Get</c> — các giao dịch thu/hoàn tiền của đơn (bảng Payments).</summary>
public class OrderPaymentRow
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    /// <summary>0 Payment (thu), 1 Refund (hoàn — PAY-04).</summary>
    public byte PaymentType { get; set; }

    public byte Method { get; set; }

    public decimal Amount { get; set; }

    /// <summary>0 Pending, 1 Succeeded, 2 Failed.</summary>
    public byte Status { get; set; }

    public string? Provider { get; set; }

    public string? ProviderTransactionId { get; set; }

    public long? ParentPaymentId { get; set; }

    public long? ReturnRequestId { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}

/// <summary>Result set 5 của <c>usp_Order_Get</c> — lịch sử chuyển trạng thái (ORD-08).</summary>
public class OrderStatusHistoryRow
{
    /// <summary><c>NULL</c> ở lần ghi đầu tiên lúc tạo đơn.</summary>
    public byte? FromStatus { get; set; }

    public byte ToStatus { get; set; }

    public string? Reason { get; set; }

    /// <summary><c>NULL</c> nếu do hệ thống (job hủy đơn quá hạn, UC-33).</summary>
    public long? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }
}

/// <summary>Result set 6 của <c>usp_Order_Get</c> — nhân viên tư vấn và tỷ lệ chia (ORD-05, KPI-02).</summary>
public class OrderSalesStaffRow
{
    public long EmployeeId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public decimal ShareRate { get; set; }

    public bool IsPrimary { get; set; }
}

/// <summary>Result set của <c>usp_Order_List</c> — kèm cột <c>TotalCount</c> (COUNT(*) OVER ()).</summary>
public class OrderListRow
{
    public long Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public long CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string? CustomerPhone { get; set; }

    public int BranchId { get; set; }

    public byte Channel { get; set; }

    public byte FulfillmentType { get; set; }

    public byte Status { get; set; }

    public decimal GrandTotal { get; set; }

    public decimal PaidAmount { get; set; }

    public byte PaymentMethod { get; set; }

    public byte PaymentStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public int TotalCount { get; set; }
}

/// <summary>Result set của <c>usp_Order_Create</c> — thông tin đơn vừa tạo (kèm hạn giữ hàng ORD-11).</summary>
public class CreatedOrderRow
{
    public long OrderId { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public byte Status { get; set; }

    public decimal GrandTotal { get; set; }

    /// <summary>Hạn giữ hàng (đơn PendingPayment); <c>NULL</c> với đơn vào thẳng Pending.</summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Result set của <c>usp_Order_AssignSerial</c> — tiến độ quét serial theo từng dòng cần serial.</summary>
public class OrderSerialProgressRow
{
    public long OrderItemId { get; set; }

    public long ProductId { get; set; }

    public int Quantity { get; set; }

    /// <summary>Số serial đã gán cho dòng (status Allocated hoặc Sold).</summary>
    public int Assigned { get; set; }
}

/// <summary>Result set của <c>usp_Order_MarkReady</c> — trạng thái đích (4 Nhận tại cửa hàng / 5 Đang giao).</summary>
public class OrderStatusRow
{
    public byte Status { get; set; }
}

/// <summary>Result set của <c>usp_Order_Cancel</c> — kèm cờ <c>RefundRequired</c> (đơn đã thu tiền, cần hoàn — PAY-04).</summary>
public class OrderCancelRow
{
    public long OrderId { get; set; }

    public byte Status { get; set; }

    public decimal PaidAmount { get; set; }

    public bool RefundRequired { get; set; }
}
