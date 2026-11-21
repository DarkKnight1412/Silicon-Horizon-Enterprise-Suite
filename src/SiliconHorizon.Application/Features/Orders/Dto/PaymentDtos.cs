namespace SiliconHorizon.Application.Features.Orders.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO thanh toán + công nợ — khớp PAY-01..05 (BRD mục 6.6) và Database/README.md mục 5.3.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// POST /api/v1/orders/{id}/payments — thu ngân xác nhận chuyển khoản / thu tiền mặt (usp_Payment_Record).
/// </summary>
/// <remarks>
/// Không có <c>provider</c>/<c>providerTransactionId</c> trong body: giao dịch do cổng thanh toán trả về đi qua
/// endpoint callback riêng, vì chỉ luồng đó mới được phép đánh dấu trùng theo mã giao dịch của cổng (PAY-02).
/// </remarks>
public class RecordPaymentRequest
{
    /// <summary>Phương thức thực thu: 0 COD, 1 Chuyển khoản, 2 Cổng thanh toán, 3 Tiền mặt/POS, 4 Công nợ.</summary>
    public byte Method { get; set; }

    /// <summary>Số tiền thu (VND); phải lớn hơn 0 (proc báo 50004 → 400).</summary>
    public decimal Amount { get; set; }

    /// <summary>Thời điểm tiền về thực tế (UTC); bỏ trống = thời điểm hiện tại.</summary>
    public DateTime? PaidAt { get; set; }
}

/// <summary>
/// POST /api/v1/payments/callback/{provider} — cổng thanh toán gọi về (usp_Payment_Record, idempotent — PAY-02).
/// </summary>
/// <remarks>
/// Phải kèm <see cref="OrderId"/> hoặc <see cref="OrderCode"/> (mã đơn nằm trong nội dung giao dịch).
/// Cùng <c>(provider, providerTransactionId)</c> gửi lại chỉ được ghi nhận một lần; API vẫn trả 200 cho cổng.
/// </remarks>
public class PaymentCallbackRequest
{
    /// <summary>Id đơn hàng — dùng khi cổng trả về id nội bộ của hệ thống.</summary>
    public long? OrderId { get; set; }

    /// <summary>Mã đơn (ví dụ <c>DH...</c>) — API tự đổi sang OrderId bằng usp_Order_Get.</summary>
    public string? OrderCode { get; set; }

    /// <summary>Số tiền cổng báo đã thu; phải lớn hơn 0 và khớp số tiền cần trả của đơn.</summary>
    public decimal Amount { get; set; }

    /// <summary>1 Chuyển khoản hoặc 2 Cổng thanh toán; bỏ trống = 2.</summary>
    public byte? Method { get; set; }

    /// <summary>Mã giao dịch của cổng — khóa chống ghi trùng; tối đa 100 ký tự.</summary>
    public string? ProviderTransactionId { get; set; }

    /// <summary>Thời điểm cổng xác nhận giao dịch (UTC); bỏ trống = thời điểm hiện tại.</summary>
    public DateTime? PaidAt { get; set; }
}

/// <summary>Kết quả ghi nhận thanh toán (usp_Payment_Record).</summary>
public class RecordedPaymentDto
{
    public long PaymentId { get; init; }

    /// <summary><c>true</c> = giao dịch đã được ghi nhận trước đó (cùng Provider + ProviderTransactionId).</summary>
    public bool IsDuplicate { get; init; }

    /// <summary>Tổng đã thu của đơn sau khi ghi nhận; <c>null</c> khi trả về do trùng giao dịch.</summary>
    public decimal? PaidAmount { get; init; }

    /// <summary>Trạng thái đơn sau khi ghi nhận; <c>null</c> khi trả về do trùng giao dịch.</summary>
    public byte? OrderStatus { get; init; }

    /// <summary>
    /// <c>true</c> = cần đối soát thủ công: tiền về cho đơn đã hủy hoặc thanh toán thừa.
    /// </summary>
    public bool NeedsReview { get; init; }
}

/// <summary>
/// POST /api/v1/orders/{id}/refunds — hoàn tiền thủ công cho đơn đã hủy hoặc giao thất bại (usp_Payment_Refund).
/// Hoàn tiền do đổi trả đi qua quy trình đổi trả (usp_Return_Refund), không dùng endpoint này.
/// </summary>
public class RefundRequest
{
    /// <summary>Số tiền hoàn (VND); phải &gt; 0 và không vượt quá số đã thanh toán của đơn (50004/50006).</summary>
    public decimal Amount { get; set; }

    /// <summary>Phương thức hoàn (nên trùng phương thức gốc — PAY-04): 0 COD, 1 Chuyển khoản, 2 Cổng thanh toán, 3 Tiền mặt, 4 Công nợ.</summary>
    public byte Method { get; set; }

    /// <summary>Lý do hoàn tiền, tối đa 300 ký tự; rỗng → 400.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Kết quả hoàn tiền — Id bản ghi hoàn trong bảng Payments.</summary>
public class RefundResultDto
{
    public long PaymentId { get; init; }
}

/// <summary>
/// GET /api/v1/customers/{id}/ledger — sao kê công nợ (usp_Customer_GetLedger): số dư + các bút toán.
/// </summary>
public class CustomerLedgerDto
{
    public long CustomerId { get; init; }

    /// <summary>Tổng công nợ hiện tại của khách (dương = khách còn nợ).</summary>
    public decimal Balance { get; init; }

    /// <summary>Hạn mức công nợ được cấp.</summary>
    public decimal CreditLimit { get; init; }

    /// <summary>Tỷ lệ đã dùng trên hạn mức (0..1; bằng 0 nếu chưa được cấp hạn mức).</summary>
    public decimal CreditUsageRatio => CreditLimit <= 0 ? 0m : Math.Round(Balance / CreditLimit, 4);

    public IReadOnlyList<CustomerLedgerEntryDto> Entries { get; init; } = Array.Empty<CustomerLedgerEntryDto>();

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 50;

    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Một bút toán công nợ của khách.</summary>
public class CustomerLedgerEntryDto
{
    public long Id { get; init; }

    /// <summary>0 Charge (phát sinh nợ), 1 Payment (giảm nợ), 2 Adjustment (điều chỉnh).</summary>
    public byte EntryType { get; init; }

    /// <summary>Dương: tăng nợ, âm: giảm nợ.</summary>
    public decimal Amount { get; init; }

    /// <summary>Hạn thanh toán của khoản ghi nợ (mua chịu).</summary>
    public DateTime? DueDate { get; init; }

    public long? OrderId { get; init; }

    public long? PaymentId { get; init; }

    public DateTime CreatedAt { get; init; }
}
