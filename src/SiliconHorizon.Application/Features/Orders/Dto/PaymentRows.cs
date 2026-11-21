namespace SiliconHorizon.Application.Features.Orders.Dto;

// Lớp ánh xạ cột result set của các proc thanh toán/công nợ (Database/03_sp_orders_payments.sql,
// Database/06_sp_hr_audit_reports.sql) — tên property PHẢI khớp đúng tên cột.

/// <summary>
/// Result set của <c>usp_Payment_Record</c>. Proc trả hai dạng:
/// <list type="bullet">
/// <item>Giao dịch đã ghi nhận trước đó (idempotent — PAY-02): chỉ có <c>PaymentId</c> và <c>IsDuplicate = 1</c>.</item>
/// <item>Ghi nhận mới: thêm <c>PaidAmount</c>, <c>OrderStatus</c>, <c>NeedsReview</c>.</item>
/// </list>
/// Vì vậy ba trường cuối là nullable (thiếu cột thì Dapper để <c>null</c>).
/// </summary>
public class PaymentRecordRow
{
    public long PaymentId { get; set; }

    /// <summary><c>true</c> = cùng (Provider, ProviderTransactionId) đã được ghi nhận trước đó, không cộng tiền lần hai.</summary>
    public bool IsDuplicate { get; set; }

    /// <summary>Tổng số tiền đã thu của đơn SAU khi ghi nhận.</summary>
    public decimal? PaidAmount { get; set; }

    /// <summary>Trạng thái đơn sau khi ghi nhận (0..10).</summary>
    public byte? OrderStatus { get; set; }

    /// <summary>
    /// <c>true</c> = tiền về cho đơn đã hủy hoặc thanh toán thừa so với GrandTotal → cần xử lý thủ công
    /// (đối soát/hoàn tiền), KHÔNG tự động chuyển trạng thái đơn.
    /// </summary>
    public bool? NeedsReview { get; set; }
}

/// <summary>Result set của <c>usp_Payment_Refund</c> — Id của bản ghi hoàn tiền vừa tạo (PAY-04).</summary>
public class RefundRow
{
    public long PaymentId { get; set; }
}

/// <summary>Result set 1 của <c>usp_Customer_GetLedger</c> — số dư công nợ và hạn mức của khách.</summary>
public class LedgerSummaryRow
{
    /// <summary>Tổng các bút toán <c>CustomerLedger</c>: dương = khách còn nợ.</summary>
    public decimal Balance { get; set; }

    /// <summary>Hạn mức công nợ được cấp (PRC-08, chỉ khách sỉ).</summary>
    public decimal CreditLimit { get; set; }
}

/// <summary>Result set 2 của <c>usp_Customer_GetLedger</c> — một bút toán công nợ, kèm <c>TotalCount</c>.</summary>
public class LedgerEntryRow
{
    public long Id { get; set; }

    /// <summary>0 Charge (phát sinh nợ), 1 Payment (giảm nợ), 2 Adjustment (điều chỉnh).</summary>
    public byte EntryType { get; set; }

    /// <summary>Dương: tăng nợ, âm: giảm nợ.</summary>
    public decimal Amount { get; set; }

    public DateTime? DueDate { get; set; }

    public long? OrderId { get; set; }

    public long? PaymentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int TotalCount { get; set; }
}
